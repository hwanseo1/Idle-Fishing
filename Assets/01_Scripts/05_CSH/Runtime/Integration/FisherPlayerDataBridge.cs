using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using JHS.Equipment;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fisher.PlayerSystems
{
    /// <summary>
    /// WJ PlayerData의 3화폐 snapshot을 Fisher 상태로 동기화하고,
    /// JHS 장비 시스템에는 골드 지갑과 CSH 강화재료 인벤토리를 주입합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FisherPlayerDataBridge : MonoBehaviour, IGoldWallet
    {
        #region Constants

        private const string WorkSceneName = "05_CSH";
        private const string DefaultPlayFabPlayerId = "player1";
        private const string FishInventoryKey = "FishInventory";
        private const string FoodInventoryKey = "FoodInventory";
        private const string IngredientInventoryKey = "IngredientInventory";
        private const string OddmentInventoryKey = "OddmentInventory";
        private const string ShopPurchaseFunctionName = "PurchaseShopItem";
        private const string PremiumCurrencyPurchaseFunctionName = "PurchasePremiumCurrencyProduct";
        private const string CookingSpeedupFunctionName = "SpeedupCooking";
        private const string BoxUseFunctionName = "UseBox";
        private const string BagCapacityExpansionFunctionName = "ExpandBagCapacity";
        private const string CollectionRewardClaimFunctionName = "ClaimCollectionReward";
        private const float BackgroundSnapshotRefreshCooldownSeconds = 5f;
        private static readonly bool CookingMutationDiagnosticsEnabled = true;
        private const int CookingMutationRawJsonLogLimit = 4000;
        private const string CookingMutationClientBuildId = "2026-06-23-csh20-cooking-incident-001";

        #endregion

        #region Dependencies

        private FisherRuntimeContext _context;
        private DataChangeListener _playerData;
        private bool _playerDataWarningLogged;
        private bool _playFabInventoryApiMissingLogged;
        private bool _playFabInventoryQueueBridgeLogged;
        private bool _stringInventoryMirrorSkippedLogged;
        private bool _playFabDataStoreWarningLogged;
        private bool _playFabInventoryGatewayStatusLogged;
        private bool _lastPlayFabInventoryGatewayReady;
        private bool _playFabCookingGatewayStatusLogged;
        private bool _lastPlayFabCookingGatewayReady;
        private bool _backgroundPlayFabSnapshotRefreshInProgress;
        private float _nextBackgroundPlayFabSnapshotRefreshTime;
        private float _suppressInventorySnapshotPullUntil;
        private float _suppressCookingSnapshotPullUntil;
        private string _lastCookingServerNowUtc = string.Empty;
        private string _lastCookingMutationAppliedAtUtc = string.Empty;
        private long _lastCookingServerNowUtcTicks;
        private long _lastCookingMutationAppliedUtcTicks;
        private int _lastCookingClaimedCookCount;
        private PlayFabGateway _cshRuntimeStateGateway;
        private EquipmentMaterialInventoryAdapter _materialInventoryAdapter;
        [Tooltip("CSH 인벤토리 변화량을 YWJ PlayFab Inventory 큐에도 전달합니다. 큐 병합/Flush 제한은 서버 게이트웨이 쪽에서 처리합니다.")]
        [SerializeField] private bool _queuePlayFabInventoryActions = true;
        [Tooltip("낚시/요리/상점 itemId 변화량이 PlayFab Inventory 큐로 들어가는 경로를 최초 1회 로그로 남깁니다.")]
        [SerializeField] private bool _logPlayFabInventoryQueueBridge = true;

        /// <summary>
        /// 장비 시스템이 조회하는 현재 골드입니다.
        /// </summary>
        public long Gold
        {
            get
            {
                if (TryResolvePlayerData(out DataChangeListener playerData))
                {
                    if (TryReadGold(playerData, out long playerGold))
                    {
                        return playerGold;
                    }
                }

                return _context != null && _context.State != null ? _context.State.softCurrency : 0;
            }
        }

        public int LastCookingClaimedCookCount => _lastCookingClaimedCookCount;

        #endregion

        #region Configuration

        /// <summary>
        /// Fisher 런타임 컨텍스트를 연결하고 현재 PlayerData 화폐를 Fisher 상태로 동기화합니다.
        /// Bootstrap 중 패널 Refresh 재진입을 막기 위해 여기서는 NotifyRuntimeChanged를 호출하지 않습니다.
        /// </summary>
        public void Configure(FisherRuntimeContext context)
        {
            _context = context;
            PullCurrenciesFromPlayerData(notify: false);
            TryInjectEquipmentWallet();
            TryInjectEquipmentMaterialInventory();
            RefreshPlayFabInventoryGatewayStatus("bootstrap");
            TryBindCshRuntimeStateGateway();
        }

        public bool IsBoundToContext(FisherRuntimeContext context)
        {
            return _context != null && ReferenceEquals(_context, context);
        }

        private void Update()
        {
            RefreshPlayFabInventoryGatewayStatus("runtime");
            TryBindCshRuntimeStateGateway();
        }

        private void OnDestroy()
        {
            UnbindCshRuntimeStateGateway();
        }

        public bool IsPlayFabInventorySellReady()
        {
            return RefreshPlayFabInventoryGatewayStatus("sell-ui");
        }

        public bool IsPlayFabBoxUseReady()
        {
            return RefreshPlayFabInventoryGatewayStatus("box-use-ui");
        }

        public bool IsPlayFabGatewayReady()
        {
            return PlayFabGateway.Instance != null;
        }

        public bool IsPlayFabCookingGatewayReady()
        {
            return RefreshPlayFabCookingGatewayStatus("cooking-ui");
        }

        public bool IsPlayFabShopGatewayReady()
        {
            PlayFabGateway gateway = PlayFabGateway.Instance;
            return gateway != null && gateway.Shop != null;
        }

        /// <summary>
        /// WJ PlayerData gold 값을 Fisher softCurrency에 반영합니다. 기존 호출 호환용 wrapper입니다.
        /// </summary>
        public bool PullGoldFromPlayerData()
        {
            return PullCurrenciesFromPlayerData();
        }

        /// <summary>
        /// WJ PlayerData의 Gold, Prism Pearl, Pirate Coin 값을 Fisher 상태에 반영합니다.
        /// </summary>
        public bool PullCurrenciesFromPlayerData(bool notify = true)
        {
            if (_context == null || _context.State == null || !TryResolvePlayerData(out DataChangeListener playerData))
            {
                return false;
            }

            if (!TryReadSnapshot(playerData, out PlayerDataSnapshot snapshot))
            {
                return false;
            }

            long nextGold = ShouldPreserveFixtureGold()
                ? (_context.State.softCurrency > snapshot.Gold ? _context.State.softCurrency : snapshot.Gold)
                : snapshot.Gold;
            long nextPrismPearl = snapshot.PrismPearl;
            long nextPirateCoin = snapshot.PirateCoin;
            int nextCurrentStage = Mathf.Max(1, snapshot.CurrentStage);
            int nextFarthestStage = Mathf.Max(nextCurrentStage, snapshot.FarthestStage);

            bool changed =
                _context.State.softCurrency != nextGold ||
                _context.State.prismPearl != nextPrismPearl ||
                _context.State.pirateCoin != nextPirateCoin ||
                _context.State.currentStage != nextCurrentStage ||
                _context.State.farthestStage != nextFarthestStage;

            _context.State.softCurrency = nextGold;
            _context.State.prismPearl = nextPrismPearl;
            _context.State.pirateCoin = nextPirateCoin;
            _context.State.currentStage = nextCurrentStage;
            _context.State.farthestStage = nextFarthestStage;

            if (notify && changed)
            {
                _context.NotifyRuntimeChanged();
            }
            return true;
        }

        /// <summary>
        /// JHS 배 시스템의 해금 상태를 도감 발견 상태로 반영합니다.
        /// 선원은 서버/JSON 기준 ID가 확정된 뒤 별도 어댑터로 연결합니다.
        /// </summary>
        public bool PullBoatCollectionDiscoveries(bool notify = true)
        {
            if (_context == null ||
                _context.State == null ||
                _context.CollectionService == null)
            {
                return false;
            }

            EquipmentManager equipmentManager = FindFirstObjectByType<EquipmentManager>();
            if (equipmentManager == null)
            {
                return false;
            }

            bool changed = false;
            foreach (BoatSkillEntry boatSkill in equipmentManager.GetBoatSkills())
            {
                if (boatSkill == null ||
                    !boatSkill.unlocked ||
                    string.IsNullOrWhiteSpace(boatSkill.id))
                {
                    continue;
                }

                ServiceResult result = _context.CollectionService.TryRegisterCatalogDiscovery(boatSkill.id);
                if (result.Success && result.MessageKey == "collection.discovery_registered")
                {
                    changed = true;
                }
            }

            if (notify && changed)
            {
                _context.NotifyRuntimeChanged();
            }

            return changed;
        }

        #endregion

        #region Gold Wallet

        /// <summary>
        /// 장비 강화 비용 차감용 Gold-only 지갑 계약입니다.
        /// </summary>
        public bool TrySpend(long amount)
        {
            if (amount < 0 || amount > int.MaxValue)
            {
                return false;
            }

            if (TryResolvePlayerData(out DataChangeListener playerData))
            {
                if (!TryReadGold(playerData, out long playerGold) || playerGold < amount)
                {
                    return false;
                }

                if (!TrySubtractGold(playerData, (int)amount))
                {
                    return false;
                }

                PullGoldFromPlayerData();
                return true;
            }

            if (_context == null || _context.State == null || _context.State.softCurrency < amount)
            {
                return false;
            }

            _context.State.softCurrency -= amount;
            _context.NotifyRuntimeChanged();
            return true;
        }

        /// <summary>
        /// 장비 재료 차감 실패 등에서 이미 차감한 Gold-only 비용을 되돌립니다.
        /// </summary>
        public void Refund(long amount)
        {
            if (amount <= 0 || amount > int.MaxValue)
            {
                return;
            }

            if (TryResolvePlayerData(out DataChangeListener playerData))
            {
                if (!TryAddGold(playerData, (int)amount))
                {
                    return;
                }

                PullGoldFromPlayerData();
                return;
            }

            if (_context == null || _context.State == null)
            {
                return;
            }

            if (!CurrencyMath.TryAdd(_context.State.softCurrency, amount, out long nextGold))
            {
                return;
            }

            _context.State.softCurrency = nextGold;
            _context.NotifyRuntimeChanged();
        }

        /// <summary>
        /// CSH 판매 보상처럼 이미 Fisher 상태에 반영된 Gold 증가분을 WJ PlayerData에도 반영합니다.
        /// </summary>
        public bool TryAddGoldToPlayerData(long amount)
        {
            if (amount <= 0 || amount > int.MaxValue)
            {
                return false;
            }

            if (!TryResolvePlayerData(out DataChangeListener playerData))
            {
                return false;
            }

            if (!TryAddGold(playerData, (int)amount))
            {
                return false;
            }

            PullGoldFromPlayerData();
            return true;
        }

        /// <summary>
        /// CSH 상점 구매처럼 이미 Fisher 상태에서 차감된 Gold 지출을 WJ PlayerData에도 반영합니다.
        /// 장비 지갑용 TrySpend와 달리 PlayerData가 없을 때 Fisher 상태를 다시 차감하지 않습니다.
        /// </summary>
        public bool TrySpendGoldFromPlayerData(long amount)
        {
            if (amount <= 0 || amount > int.MaxValue)
            {
                return false;
            }

            if (!TryResolvePlayerData(out DataChangeListener playerData))
            {
                return false;
            }

            if (!TryReadGold(playerData, out long playerGold) || playerGold < amount)
            {
                return false;
            }

            if (!TrySubtractGold(playerData, (int)amount))
            {
                return false;
            }

            PullGoldFromPlayerData();
            return true;
        }

        /// <summary>
        /// Fisher 서비스 결과의 itemId 수량 변화를 WJ 문자열 인벤토리와 PlayFab 큐에 반영합니다.
        /// 기존 int 인벤토리 계약은 건드리지 않고, 새 itemId 계약만 미러링합니다.
        /// </summary>
        public bool SyncItemDeltasToPlayerData(ServiceResult result)
        {
            if (result == null || !result.Success || result.ItemDeltas.Count == 0)
            {
                return true;
            }

            SuppressInventorySnapshotPull();

            bool allSynced = true;
            for (int i = 0; i < result.ItemDeltas.Count; i++)
            {
                ItemDelta delta = result.ItemDeltas[i];
                if (delta == null || delta.CountDelta == 0)
                {
                    continue;
                }

                allSynced &= TrySyncItemDeltaToPlayerData(delta.ItemId, delta.CountDelta);
            }

            return allSynced;
        }

        /// <summary>
        /// YWJ PlayFabDataStore가 저장한 Player_Playfab.json inventory snapshot을 CSH 가방 표시 상태에 반영합니다.
        /// 로그인 전/스토어 없음이면 기존 로컬 상태를 보존하고, 로그인 후 빈 inventory snapshot은 서버 기준으로 반영합니다.
        /// </summary>
        public bool PullInventoryFromPlayFabDataStore(bool notify = true, bool force = false)
        {
            if (_context == null ||
                _context.InventoryService == null ||
                _context.BuildResult == null ||
                _context.BuildResult.Catalog == null)
            {
                return false;
            }

            if (!TryGetPlayFabSnapshotPlayer(out PlayerInfo player) ||
                player.inventory == null ||
                player.inventory.inventoryItems == null)
            {
                return false;
            }

            if (!force && IsInventorySnapshotPullSuppressed())
            {
                return false;
            }

            Dictionary<string, int> validCounts = new Dictionary<string, int>();
            foreach (KeyValuePair<string, InventoryInfo> pair in player.inventory.inventoryItems)
            {
                string itemId = pair.Key;
                InventoryInfo item = pair.Value;
                if (string.IsNullOrWhiteSpace(itemId) || item == null || item.itemCount <= 0)
                {
                    continue;
                }

                if (!_context.BuildResult.Catalog.TryGetItem(itemId, out ItemDefinition definition) ||
                    definition == null ||
                    !definition.IsEnabled ||
                    !definition.Stackable)
                {
                    LogPlayFabDataStoreWarning("인벤토리 snapshot에서 CSH 카탈로그에 반영할 수 없는 itemId를 건너뜁니다: " + itemId);
                    continue;
                }

                validCounts[itemId] = item.itemCount;
            }

            ServiceResult result = _context.InventoryService.ReplaceStackedInventorySnapshot(validCounts);
            if (result == null || !result.Success)
            {
                LogPlayFabDataStoreWarning("인벤토리 snapshot 반영 실패: " + (result == null ? "null result" : result.MessageKey));
                return false;
            }

            _context.MarkPlayFabSnapshotHydration(currencyHydrated: false, inventoryHydrated: true, source: "PullInventory");

            if (notify)
            {
                _context.NotifyRuntimeChanged();
            }

            return true;
        }

        /// <summary>
        /// YWJ PlayFabDataStore의 currency snapshot을 CSH 재화 표시 상태에 반영합니다.
        /// 로그인 전 기본 0값만 있는 snapshot은 프리뷰 값을 덮지 않도록 건너뜁니다.
        /// </summary>
        public bool PullCurrenciesFromPlayFabDataStore(bool notify = true)
        {
            if (_context == null || _context.State == null)
            {
                return false;
            }

            if (!TryGetPlayFabSnapshotPlayer(out PlayerInfo player) || player.currency == null)
            {
                return false;
            }

            long nextGold = player.currency.gold;
            long nextPrismPearl = player.currency.prismPearl;
            long nextPirateCoin = player.currency.pirateCoin;

            bool changed =
                _context.State.softCurrency != nextGold ||
                _context.State.prismPearl != nextPrismPearl ||
                _context.State.pirateCoin != nextPirateCoin;

            _context.State.softCurrency = nextGold;
            _context.State.prismPearl = nextPrismPearl;
            _context.State.pirateCoin = nextPirateCoin;
            _context.MarkPlayFabSnapshotHydration(currencyHydrated: true, inventoryHydrated: false, source: "PullCurrencies");

            if (notify && changed)
            {
                _context.NotifyRuntimeChanged();
            }

            return true;
        }

        /// <summary>
        /// YWJ PlayFabDataStore의 cookSlot snapshot을 CSH 요리 표시 상태에 반영합니다.
        /// 제작 시작/취소/완료/가속은 서버 권한이므로 로컬 실행하지 않고, UI용 진행 상태만 교체합니다.
        /// </summary>
        public bool PullCookingFromPlayFabDataStore(bool notify = true, bool force = false)
        {
            if (_context == null || _context.CookingService == null)
            {
                return false;
            }

            PlayFabDataStore store = PlayFabDataStore.Instance;
            if (store == null || !store.HasFreshCookingSnapshotThisSession)
            {
                return false;
            }

            if (!force && IsCookingSnapshotPullSuppressed())
            {
                Debug.Log("[FISHER_COOK_DIAG][Bridge][SnapshotPull] skipped suppressedByRecentMutation serverNowUtc=" +
                          _lastCookingServerNowUtc +
                          ", mutationAppliedAtUtc=" + _lastCookingMutationAppliedAtUtc +
                          ", active=" + ActiveCookingStateForDiagnostics());
                return false;
            }

            if (!TryGetPlayFabSnapshotPlayer(out PlayerInfo player) ||
                player.cookSlot == null ||
                player.cookSlot.cookSlots == null)
            {
                return false;
            }

            List<ActiveRecipeState> activeRecipes = new List<ActiveRecipeState>();
            int openedSlotCount = 0;
            foreach (KeyValuePair<string, CookingSlotInfo> pair in player.cookSlot.cookSlots)
            {
                CookingSlotInfo slot = pair.Value;
                if (slot == null)
                {
                    continue;
                }

                if (slot.isOpened)
                {
                    openedSlotCount++;
                }

                if (slot.job == null ||
                    string.IsNullOrWhiteSpace(slot.job.recipeId) ||
                    slot.job.totalCount <= slot.job.claimedCount)
                {
                    continue;
                }

                if (!TryBuildActiveRecipeFromServerSlot(pair.Key, slot.job, out ActiveRecipeState active))
                {
                    LogPlayFabDataStoreWarning("요리 snapshot에서 변환할 수 없는 cookSlot을 건너뜁니다: slot=" + pair.Key);
                    continue;
                }

                activeRecipes.Add(active);
            }

            ServiceResult result = _context.CookingService.ReplaceServerCookingSnapshot(activeRecipes, openedSlotCount);
            if (result == null || !result.Success)
            {
                LogPlayFabDataStoreWarning("요리 snapshot 반영 실패: " + (result == null ? "null result" : result.MessageKey));
                return false;
            }

            if (notify)
            {
                _context.NotifyRuntimeChanged();
            }

            return true;
        }

        /// <summary>
        /// UI 진입 시 서버 로컬 snapshot을 한 번에 표시 상태로 당겨옵니다.
        /// 이 메서드는 PlayFab 서버 호출을 하지 않고 PlayFabDataStore의 로컬 JSON만 읽습니다.
        /// </summary>
        public bool PullDisplaySnapshotsFromPlayFabDataStore(bool notify = true, bool forceInventory = false, bool forceCooking = false)
        {
            bool changedOrApplied = false;
            changedOrApplied |= PullCurrenciesFromPlayFabDataStore(notify: false);
            changedOrApplied |= PullInventoryFromPlayFabDataStore(notify: false, force: forceInventory);
            changedOrApplied |= PullCookingFromPlayFabDataStore(notify: false, force: forceCooking);

            if (notify && changedOrApplied)
            {
                _context?.NotifyRuntimeChanged();
            }

            return changedOrApplied;
        }

        public bool RequiresBagSnapshotHydration()
        {
            return PlayFabClientAPI.IsClientLoggedIn() || TryGetPlayFabSnapshotPlayer(out _);
        }

        public bool TryHydrateBagSnapshotsFromPlayFabDataStore(
            bool forceInventory,
            bool notify,
            out bool currencyApplied,
            out bool inventoryApplied,
            out string blockedReason)
        {
            currencyApplied = false;
            inventoryApplied = false;
            blockedReason = string.Empty;

            bool playFabLoggedIn = PlayFabClientAPI.IsClientLoggedIn();
            bool playFabSnapshotAvailable = TryGetPlayFabSnapshotPlayer(out _);

            if (_context == null ||
                _context.State == null ||
                _context.InventoryService == null ||
                _context.BuildResult == null ||
                _context.BuildResult.Catalog == null)
            {
                blockedReason = "FisherRuntimeContext binding 준비 중";
                return false;
            }

            if (!playFabLoggedIn && !playFabSnapshotAvailable)
            {
                blockedReason = "PlayFab snapshot not required";
                return true;
            }

            if (!playFabSnapshotAvailable)
            {
                blockedReason = "PlayFabDataStore snapshot 준비 중";
                return false;
            }

            currencyApplied = PullCurrenciesFromPlayFabDataStore(notify: false);
            inventoryApplied = PullInventoryFromPlayFabDataStore(notify: false, force: forceInventory);

            if (notify && (currencyApplied || inventoryApplied))
            {
                _context.NotifyRuntimeChanged();
            }

            if (!currencyApplied || !inventoryApplied || !_context.PlayerSnapshotHydrated)
            {
                blockedReason = "PlayFabDataStore snapshot hydration 미완료";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 로컬 지급/서버 인벤토리 요청 직후 낡은 PlayFabDataStore inventory snapshot이 CSH 가방을 되덮지 않게 합니다.
        /// </summary>
        public void SuppressInventorySnapshotPull()
        {
            _suppressInventorySnapshotPullUntil = Mathf.Max(
                _suppressInventorySnapshotPullUntil,
                Time.unscaledTime + FisherServerMutationPolicy.SnapshotPullSuppressSeconds);
        }

        private bool IsInventorySnapshotPullSuppressed()
        {
            return Time.unscaledTime < _suppressInventorySnapshotPullUntil;
        }

        private void SuppressCookingSnapshotPull()
        {
            _suppressCookingSnapshotPullUntil = Mathf.Max(
                _suppressCookingSnapshotPullUntil,
                Time.unscaledTime + FisherServerMutationPolicy.SnapshotPullSuppressSeconds);
        }

        private bool IsCookingSnapshotPullSuppressed()
        {
            return Time.unscaledTime < _suppressCookingSnapshotPullUntil;
        }

        /// <summary>
        /// 판매는 서버가 sellGold/보유 수량을 검증해야 하므로 로컬 판매 로직을 거치지 않고 PlayFab 인벤토리 큐로 요청합니다.
        /// 성공 콜백은 서버 응답 이후 PlayFabDataStore를 다시 읽어 CSH 표시 snapshot을 갱신합니다.
        /// </summary>
        public bool TryRequestInventorySell(string itemId, int amount, Action onApplied = null, Action<string> onRejected = null)
        {
            if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
            {
                return false;
            }

            Dictionary<string, int> sellCounts = new Dictionary<string, int>
            {
                [itemId] = amount
            };
            return TryRequestInventorySellBatch(sellCounts, onApplied, onRejected);
        }

        /// <summary>
        /// 여러 아이템 판매 요청을 서버 inventoryKey별로 묶어 Flush합니다. 로컬 인벤토리/골드는 직접 차감하거나 지급하지 않습니다.
        /// </summary>
        public bool TryRequestInventorySellBatch(IReadOnlyDictionary<string, int> sellCounts, Action onApplied = null, Action<string> onRejected = null)
        {
            if (sellCounts == null || sellCounts.Count == 0)
            {
                return false;
            }

            return TryFlushPendingInventoryBeforeServerMutation(
                "InventorySell",
                () =>
                {
                    if (!TryQueueInventorySellBatch(sellCounts, onApplied, onRejected))
                    {
                        onRejected?.Invoke("InventorySell 요청 실패");
                    }
                },
                onRejected);
        }

        private bool TryQueueInventorySellBatch(IReadOnlyDictionary<string, int> sellCounts, Action onApplied = null, Action<string> onRejected = null)
        {
            if (sellCounts == null || sellCounts.Count == 0)
            {
                return false;
            }

            if (!RefreshPlayFabInventoryGatewayStatus("sell-request", out PlayFabGateway gateway, out InventoryGateway inventory))
            {
                return false;
            }

            HashSet<string> touchedInventoryKeys = new HashSet<string>();
            foreach (KeyValuePair<string, int> pair in sellCounts)
            {
                string itemId = pair.Key;
                int amount = pair.Value;
                if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
                {
                    continue;
                }

                if (!TryGetInventoryKeyByItemId(itemId, out string inventoryKey))
                {
                    LogPlayFabDataStoreWarning("판매 요청을 보낼 수 없는 itemId prefix입니다: " + itemId);
                    continue;
                }

                inventory.Sell(itemId, amount);
                SuppressInventorySnapshotPull();
                touchedInventoryKeys.Add(inventoryKey);
                LogPlayFabInventoryQueueBridge("Sell", itemId, amount);
            }

            if (touchedInventoryKeys.Count == 0)
            {
                return false;
            }

            bool refreshStarted = false;
            List<string> pendingInventoryKeys = new List<string>(touchedInventoryKeys);
            int pendingFlushIndex = 0;
            Action flushNextInventoryKey = null;
            Action<ExecuteCloudScriptResult, string> markApplied = (result, inventoryKey) =>
            {
                if (refreshStarted)
                {
                    return;
                }

                string operation = "InventorySell." + inventoryKey;
                if (!TryReadMutationSuccess(gateway, result, operation, out string rejectMessage, out CloudScriptMutationResponse response))
                {
                    refreshStarted = true;
                    HandleRejectedMutation(gateway, operation, response, onRejected, rejectMessage);
                    return;
                }

                ApplyCloudScriptMutationResponse(operation, response, allowDeltaFallback: true);
                flushNextInventoryKey?.Invoke();
            };
            Action<string, string> markRejected = (message, inventoryKey) =>
            {
                if (refreshStarted)
                {
                    return;
                }

                refreshStarted = true;
                string rejectMessage = string.IsNullOrWhiteSpace(message)
                    ? "InventorySell." + inventoryKey + " 서버 요청 실패"
                    : "InventorySell." + inventoryKey + " " + message;
                HandleRejectedMutation(gateway, "InventorySell." + inventoryKey, null, onRejected, rejectMessage);
            };

            flushNextInventoryKey = () =>
            {
                if (refreshStarted)
                {
                    return;
                }

                if (pendingFlushIndex >= pendingInventoryKeys.Count)
                {
                    refreshStarted = true;
                    Debug.Log("[FisherPlayerDataBridge] PlayFab Inventory Sell Flush callbacks completed serially. " +
                              "Refreshing server snapshot before applying CSH bag UI.");
                    RefreshSnapshotsInBackground(
                        gateway,
                        operation: "InventorySell",
                        pullDisplaySnapshots: true,
                        force: true,
                        onCompleted: onApplied);
                    return;
                }

                string capturedInventoryKey = pendingInventoryKeys[pendingFlushIndex];
                pendingFlushIndex++;
                try
                {
                    inventory.Flush(
                        capturedInventoryKey,
                        result => markApplied(result, capturedInventoryKey),
                        message => markRejected(message, capturedInventoryKey));
                }
                catch (Exception exception)
                {
                    markRejected("Flush 예외: " + exception.Message, capturedInventoryKey);
                }
            };

            flushNextInventoryKey();

            return true;
        }

        public bool TryRequestBoxUse(
            string boxItemId,
            int count,
            string selectedItemId = null,
            string selectedInventoryKey = null,
            Action onApplied = null,
            Action<string> onRejected = null)
        {
            if (string.IsNullOrWhiteSpace(boxItemId) || count <= 0)
            {
                return false;
            }

            return TryFlushPendingInventoryBeforeServerMutation(
                BoxUseFunctionName,
                () =>
                {
                    if (!TrySendBoxUseRequest(boxItemId, count, selectedItemId, selectedInventoryKey, onApplied, onRejected))
                    {
                        onRejected?.Invoke("UseBox 요청 실패");
                    }
                },
                onRejected);
        }

        private bool TrySendBoxUseRequest(
            string boxItemId,
            int count,
            string selectedItemId,
            string selectedInventoryKey,
            Action onApplied,
            Action<string> onRejected)
        {
            if (string.IsNullOrWhiteSpace(boxItemId) || count <= 0)
            {
                return false;
            }

            if (!RefreshPlayFabInventoryGatewayStatus("box-use-request", out PlayFabGateway gateway, out InventoryGateway inventory))
            {
                return false;
            }

            string requestId = Guid.NewGuid().ToString("N");
            object requestArgs = new
            {
                boxItemId = boxItemId,
                count = count,
                selectedItemId = selectedItemId,
                selectedInventoryKey = selectedInventoryKey,
                requestId = requestId
            };
            return TrySendPlayFabMutationRequest(
                BoxUseFunctionName,
                () =>
                {
                    inventory.UseBox(
                        boxItemId,
                        count,
                        selectedItemId,
                        selectedInventoryKey,
                        requestId,
                        result => HandlePlayFabMutationResult(gateway, result, BoxUseFunctionName, requestArgs, onApplied, onRejected),
                        error => HandlePlayFabMutationError(gateway, error, BoxUseFunctionName, onRejected),
                        forwardScriptErrors: true);

                    Debug.Log("[FisherPlayerDataBridge] Requested PlayFab Inventory UseBox: boxItemId=" + boxItemId +
                              ", count=" + count +
                              (string.IsNullOrWhiteSpace(selectedItemId) ? string.Empty : ", selectedItemId=" + selectedItemId));
                },
                onRejected);
        }

        public bool TryRequestCookingStart(int slotIndex, string recipeId, int totalCount, Action onApplied = null, Action<string> onRejected = null)
        {
            if (slotIndex < 0 || string.IsNullOrWhiteSpace(recipeId) || totalCount <= 0)
            {
                return false;
            }

            if (PlayFabGateway.Instance == null)
            {
                LogPlayFabDataStoreWarning("요리 시작 요청을 보낼 PlayFabGateway.Instance가 없습니다.");
                return false;
            }

            return TryFlushPendingInventoryBeforeServerMutation(
                "StartCooking",
                () =>
                {
                    if (!TrySendCookingStartRequest(slotIndex, recipeId, totalCount, onApplied, onRejected))
                    {
                        onRejected?.Invoke("StartCooking 요청 실패");
                    }
                },
                onRejected);
        }

        private bool TrySendCookingStartRequest(int slotIndex, string recipeId, int totalCount, Action onApplied, Action<string> onRejected)
        {
            return TryRequestDirectCloudScriptMutation(
                "StartCooking",
                new
                {
                    slotIndex = slotIndex,
                    recipeId = recipeId,
                    totalCount = totalCount,
                    clientBuildId = CookingMutationClientBuildId,
                    requestId = Guid.NewGuid().ToString("N")
                },
                onApplied,
                onRejected);
        }

        public bool TryRequestCookingClaim(int slotIndex, Action onApplied = null, Action<string> onRejected = null)
        {
            return TryRequestCookingClaim(slotIndex, recipeId: null, activeStartedUtcTicks: 0L, onApplied, onRejected);
        }

        public bool TryRequestCookingClaim(int slotIndex, string recipeId, Action onApplied = null, Action<string> onRejected = null)
        {
            return TryRequestCookingClaim(slotIndex, recipeId, activeStartedUtcTicks: 0L, onApplied, onRejected);
        }

        public bool TryRequestCookingClaim(int slotIndex, string recipeId, long activeStartedUtcTicks, Action onApplied = null, Action<string> onRejected = null)
        {
            if (slotIndex < 0)
            {
                return false;
            }

            if (PlayFabGateway.Instance == null)
            {
                LogPlayFabDataStoreWarning("요리 수령 요청을 보낼 PlayFabGateway.Instance가 없습니다.");
                return false;
            }

            _lastCookingClaimedCookCount = 0;
            Dictionary<string, object> args = BuildCookingMutationArgs(slotIndex, recipeId, activeStartedUtcTicks);
            LogCookingMutationDiagnostic("ClaimCooking", "builtArgs=" + JsonForDiagnostics(args));
            return TryRequestDirectCloudScriptMutation(
                "ClaimCooking",
                args,
                onApplied,
                onRejected);
        }

        public bool TryRequestCookingCancel(int slotIndex, Action onApplied = null, Action<string> onRejected = null)
        {
            return TryRequestCookingCancel(slotIndex, recipeId: null, activeStartedUtcTicks: 0L, onApplied, onRejected);
        }

        public bool TryRequestCookingCancel(int slotIndex, string recipeId, Action onApplied = null, Action<string> onRejected = null)
        {
            return TryRequestCookingCancel(slotIndex, recipeId, activeStartedUtcTicks: 0L, onApplied, onRejected);
        }

        public bool TryRequestCookingCancel(int slotIndex, string recipeId, long activeStartedUtcTicks, Action onApplied = null, Action<string> onRejected = null)
        {
            if (slotIndex < 0)
            {
                return false;
            }

            if (PlayFabGateway.Instance == null)
            {
                LogPlayFabDataStoreWarning("요리 취소 요청을 보낼 PlayFabGateway.Instance가 없습니다.");
                return false;
            }

            Dictionary<string, object> args = BuildCookingMutationArgs(slotIndex, recipeId, activeStartedUtcTicks);
            LogCookingMutationDiagnostic("CancelCooking", "builtArgs=" + JsonForDiagnostics(args));
            return TryRequestDirectCloudScriptMutation(
                "CancelCooking",
                args,
                onApplied,
                onRejected);
        }

        public bool TryRequestCookingSpeedup(int slotIndex, string itemId, int seconds, Action onApplied = null, Action<string> onRejected = null)
        {
            return TryRequestCookingSpeedup(slotIndex, itemId, seconds, recipeId: null, activeStartedUtcTicks: 0L, onApplied, onRejected);
        }

        public bool TryRequestCookingSpeedup(int slotIndex, string itemId, int seconds, string recipeId, Action onApplied = null, Action<string> onRejected = null)
        {
            return TryRequestCookingSpeedup(slotIndex, itemId, seconds, recipeId, activeStartedUtcTicks: 0L, onApplied, onRejected);
        }

        public bool TryRequestCookingSpeedup(int slotIndex, string itemId, int seconds, string recipeId, long activeStartedUtcTicks, Action onApplied = null, Action<string> onRejected = null)
        {
            if (slotIndex < 0 || string.IsNullOrWhiteSpace(itemId) || seconds <= 0)
            {
                return false;
            }

            if (PlayFabGateway.Instance == null)
            {
                LogPlayFabDataStoreWarning("요리 가속 요청을 보낼 PlayFabGateway.Instance가 없습니다.");
                return false;
            }

            return TryFlushPendingInventoryBeforeServerMutation(
                CookingSpeedupFunctionName,
                () =>
                {
                    if (!TrySendCookingSpeedupRequest(slotIndex, itemId, seconds, recipeId, activeStartedUtcTicks, onApplied, onRejected))
                    {
                        onRejected?.Invoke("SpeedupCooking 요청 실패");
                    }
                },
                onRejected);
        }

        private bool TrySendCookingSpeedupRequest(
            int slotIndex,
            string itemId,
            int seconds,
            string recipeId,
            long activeStartedUtcTicks,
            Action onApplied,
            Action<string> onRejected)
        {
            Dictionary<string, object> args = BuildCookingMutationArgs(slotIndex, recipeId, activeStartedUtcTicks);
            args["itemId"] = itemId;
            args["seconds"] = seconds;
            LogCookingMutationDiagnostic(CookingSpeedupFunctionName, "builtArgs=" + JsonForDiagnostics(args));
            return TryRequestDirectCloudScriptMutation(
                CookingSpeedupFunctionName,
                args,
                onApplied,
                onRejected);
        }

        private bool TryFlushPendingInventoryBeforeServerMutation(string operation, Action onReady, Action<string> onRejected)
        {
            if (onReady == null)
            {
                return false;
            }

            PlayFabGateway gateway = PlayFabGateway.Instance;
            InventoryGateway inventory = gateway == null ? null : gateway.Inventory;
            if (gateway == null || inventory == null)
            {
                LogPlayFabDataStoreWarning(operation + " 전에 PlayFabGateway.Inventory를 찾을 수 없습니다.");
                return false;
            }

            try
            {
                inventory.FlushAll(
                    _ =>
                    {
                        Debug.Log("[FisherPlayerDataBridge] Pending inventory queue flushed before " + operation + ".");
                        onReady.Invoke();
                    },
                    message =>
                    {
                        string rejectMessage = string.IsNullOrWhiteSpace(message)
                            ? operation + " 전 inventory queue flush 실패"
                            : operation + " 전 inventory queue flush 실패: " + message;
                        onRejected?.Invoke(rejectMessage);
                    });
                return true;
            }
            catch (Exception exception)
            {
                onRejected?.Invoke(operation + " 전 inventory queue flush 예외: " + exception.Message);
                return true;
            }
        }

        public bool TryRequestShopPurchase(string shopItemId, Action onApplied = null, Action<string> onRejected = null)
        {
            if (string.IsNullOrWhiteSpace(shopItemId))
            {
                return false;
            }

            PlayFabGateway gateway = PlayFabGateway.Instance;
            if (gateway == null || gateway.Shop == null)
            {
                LogPlayFabDataStoreWarning("상점 구매 요청을 보낼 PlayFabGateway.Shop이 없습니다.");
                return false;
            }

            string requestId = Guid.NewGuid().ToString("N");
            object requestArgs = new
            {
                shopItemId = shopItemId,
                requestId = requestId
            };
            return TrySendPlayFabMutationRequest(
                ShopPurchaseFunctionName,
                () =>
                {
                    gateway.Shop.PurchaseShopItem(
                        shopItemId,
                        requestId,
                        result => HandlePlayFabMutationResult(gateway, result, ShopPurchaseFunctionName, requestArgs, onApplied, onRejected),
                        error => HandlePlayFabMutationError(gateway, error, ShopPurchaseFunctionName, onRejected),
                        forwardScriptErrors: true);
                    Debug.Log("[FisherPlayerDataBridge] Requested PlayFab Shop PurchaseShopItem: shopItemId=" + shopItemId);
                },
                onRejected);
        }

        public bool TryRequestPremiumCurrencyProductPurchase(string productId, Action onApplied = null, Action<string> onRejected = null)
        {
            if (string.IsNullOrWhiteSpace(productId))
            {
                return false;
            }

            return TryRequestDirectCloudScriptMutation(
                PremiumCurrencyPurchaseFunctionName,
                new
                {
                    productId = productId,
                    requestId = Guid.NewGuid().ToString("N")
                },
                onApplied,
                onRejected);
        }

        public bool TryRequestBagCapacityExpansion(Action onApplied = null, Action<string> onRejected = null)
        {
            return TryRequestDirectCloudScriptMutation(
                BagCapacityExpansionFunctionName,
                new
                {
                    requestId = Guid.NewGuid().ToString("N")
                },
                onApplied,
                onRejected);
        }

        public bool TryRequestCollectionRewardClaim(string rewardId, Action onApplied = null, Action<string> onRejected = null)
        {
            if (string.IsNullOrWhiteSpace(rewardId))
            {
                return false;
            }

            return TryRequestDirectCloudScriptMutation(
                CollectionRewardClaimFunctionName,
                new
                {
                    rewardId = rewardId,
                    requestId = Guid.NewGuid().ToString("N")
                },
                onApplied,
                onRejected);
        }

        public bool TrySyncItemDeltaToPlayerData(string itemId, int countDelta)
        {
            if (string.IsNullOrWhiteSpace(itemId) || countDelta == 0)
            {
                return false;
            }

            bool playerDataSynced = TryApplyItemDeltaToPlayerData(itemId, countDelta);
            bool playFabQueued = !_queuePlayFabInventoryActions || TryQueuePlayFabInventoryDelta(itemId, countDelta);
            return playerDataSynced && playFabQueued;
        }

        #endregion

        #region Resolve Helpers

        private bool TryResolvePlayerData(out DataChangeListener playerData)
        {
            if (_playerData == null)
            {
                _playerData = DataChangeListener.Instance != null
                    ? DataChangeListener.Instance
                    : FindFirstObjectByType<DataChangeListener>();
            }

            playerData = _playerData;
            return playerData != null;
        }

        private bool TryReadSnapshot(DataChangeListener playerData, out PlayerDataSnapshot snapshot)
        {
            snapshot = default;
            if (playerData == null)
            {
                return false;
            }

            try
            {
                snapshot = new PlayerDataSnapshot
                {
                    Gold = playerData.Try_GetGold(),
                    PrismPearl = playerData.Try_GetPrismPearl(),
                    PirateCoin = playerData.Try_GetPirateCoin(),
                    CurrentStage = playerData.Try_GetCurrentStage(),
                    FarthestStage = playerData.Try_GetFarthestStage()
                };
                return true;
            }
            catch (System.Exception exception)
            {
                LogPlayerDataWarning(exception);
                return false;
            }
        }

        private bool TryGetPlayFabSnapshotPlayer(out PlayerInfo player)
        {
            player = null;
            PlayFabDataStore store = PlayFabDataStore.Instance;
            if (store == null ||
                store.Data == null ||
                store.Data.players == null ||
                !store.Data.players.TryGetValue(DefaultPlayFabPlayerId, out player) ||
                player == null ||
                !HasUsablePlayFabSnapshot(player))
            {
                return false;
            }

            return true;
        }

        private static Dictionary<string, object> BuildCookingMutationArgs(int slotIndex, string recipeId, long activeStartedUtcTicks)
        {
            Dictionary<string, object> args = new Dictionary<string, object>
            {
                ["slotIndex"] = slotIndex,
                ["clientBuildId"] = CookingMutationClientBuildId,
                ["requestId"] = Guid.NewGuid().ToString("N")
            };
            if (!string.IsNullOrWhiteSpace(recipeId))
            {
                args["recipeId"] = recipeId;
            }

            if (TryFormatUtcTicks(activeStartedUtcTicks, out string activeStartedAtUtc))
            {
                args["activeStartedAtUtc"] = activeStartedAtUtc;
            }

            return args;
        }

        private static bool TryFormatUtcTicks(long ticks, out string utcIsoString)
        {
            utcIsoString = string.Empty;
            if (ticks <= 0L)
            {
                return false;
            }

            try
            {
                DateTime utc = DateTime.SpecifyKind(new DateTime(ticks), DateTimeKind.Utc);
                utcIsoString = utc.ToString("o", CultureInfo.InvariantCulture);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        private static bool TryReadUtcTicks(string utcText, out long ticks)
        {
            ticks = 0L;
            if (string.IsNullOrWhiteSpace(utcText))
            {
                return false;
            }

            string text = utcText.Trim();
            if (TryReadIsoUtcTicks(text, out ticks))
            {
                return true;
            }

            if (!DateTimeOffset.TryParse(
                    text,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out DateTimeOffset parsed))
            {
                return false;
            }

            ticks = parsed.UtcDateTime.Ticks;
            return true;
        }

        private static bool TryReadIsoUtcTicks(string text, out long ticks)
        {
            ticks = 0L;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string body = text.EndsWith("Z", StringComparison.OrdinalIgnoreCase)
                ? text.Substring(0, text.Length - 1)
                : text;
            int separatorIndex = body.IndexOf('T');
            if (separatorIndex < 0)
            {
                separatorIndex = body.IndexOf(' ');
            }

            if (separatorIndex <= 0 || separatorIndex >= body.Length - 1)
            {
                return false;
            }

            string datePart = body.Substring(0, separatorIndex);
            string timePart = body.Substring(separatorIndex + 1);
            string fractionPart = string.Empty;
            int fractionIndex = timePart.IndexOf('.');
            if (fractionIndex >= 0)
            {
                fractionPart = timePart.Substring(fractionIndex + 1);
                timePart = timePart.Substring(0, fractionIndex);
            }

            if (!DateTime.TryParseExact(
                    datePart + "T" + timePart,
                    "yyyy-MM-dd'T'HH:mm:ss",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out DateTime baseUtc))
            {
                return false;
            }

            long fractionTicks = 0L;
            if (!string.IsNullOrEmpty(fractionPart))
            {
                if (fractionPart.Length > 7)
                {
                    fractionPart = fractionPart.Substring(0, 7);
                }

                for (int i = 0; i < fractionPart.Length; i++)
                {
                    if (!char.IsDigit(fractionPart[i]))
                    {
                        return false;
                    }
                }

                string paddedFraction = fractionPart.PadRight(7, '0');
                if (!long.TryParse(paddedFraction, NumberStyles.Integer, CultureInfo.InvariantCulture, out fractionTicks))
                {
                    return false;
                }
            }

            ticks = DateTime.SpecifyKind(baseUtc, DateTimeKind.Utc).Ticks + fractionTicks;
            return true;
        }

        private static bool HasUsablePlayFabSnapshot(PlayerInfo player)
        {
            if (player == null)
            {
                return false;
            }

            if (player.inventory?.inventoryItems != null)
            {
                return true;
            }

            if (player.cookSlot?.cookSlots != null && player.cookSlot.cookSlots.Count > 0)
            {
                return true;
            }

            if (player.crew?.crews != null && player.crew.crews.Count > 0)
            {
                return true;
            }

            if (player.crewSlot?.crewSlots != null && player.crewSlot.crewSlots.Count > 0)
            {
                return true;
            }

            if (player.ship?.ships != null && player.ship.ships.Count > 0)
            {
                return true;
            }

            if (player.equipment?.equipments != null && player.equipment.equipments.Count > 0)
            {
                return true;
            }

            return player.currency != null &&
                   (player.currency.gold != 0 ||
                    player.currency.prismPearl != 0 ||
                    player.currency.pirateCoin != 0);
        }

        private static bool TryBuildActiveRecipeFromServerSlot(string slotKey, CookingJobInfo job, out ActiveRecipeState active)
        {
            active = null;
            if (job == null ||
                string.IsNullOrWhiteSpace(job.recipeId) ||
                job.totalCount <= job.claimedCount)
            {
                return false;
            }

            return TryBuildActiveRecipeFromServerValues(
                slotKey,
                job.recipeId,
                job.totalCount,
                job.claimedCount,
                job.durationSec,
                job.startedAtUtc,
                out active);
        }

        private static bool TryBuildActiveRecipeFromServerSlot(string slotKey, JObject jobObject, out ActiveRecipeState active)
        {
            active = null;
            if (jobObject == null)
            {
                return false;
            }

            if (TryBuildActiveRecipeFromServerValues(
                slotKey,
                ReadString(jobObject["recipeId"]),
                ReadInt(jobObject["totalCount"], 0),
                ReadInt(jobObject["claimedCount"], 0),
                ReadInt(jobObject["durationSec"], 0),
                ReadString(jobObject["startedAtUtc"]),
                out active))
            {
                return true;
            }

            try
            {
                CookingJobInfo typedJob = jobObject.ToObject<CookingJobInfo>();
                return TryBuildActiveRecipeFromServerSlot(slotKey, typedJob, out active);
            }
            catch (Exception)
            {
                active = null;
                return false;
            }
        }

        private static bool TryBuildActiveRecipeFromServerValues(
            string slotKey,
            string recipeId,
            int totalCount,
            int claimedCount,
            int durationSec,
            string startedAtUtc,
            out ActiveRecipeState active)
        {
            active = null;
            if (string.IsNullOrWhiteSpace(recipeId) ||
                totalCount <= claimedCount)
            {
                return false;
            }

            if (!TryReadUtcTicks(startedAtUtc, out long startedUtcTicks))
            {
                return false;
            }

            int slotIndex = 0;
            if (!string.IsNullOrWhiteSpace(slotKey) &&
                !int.TryParse(slotKey, NumberStyles.Integer, CultureInfo.InvariantCulture, out slotIndex))
            {
                slotIndex = 0;
            }

            int safeClaimedCount = Math.Max(0, claimedCount);
            int remainingCount = Math.Max(1, totalCount - safeClaimedCount);
            int safeDurationSec = Math.Max(0, durationSec);
            long durationTicks = TimeSpan.FromSeconds(safeDurationSec).Ticks;
            long nextCompleteTicks = durationTicks <= 0
                ? startedUtcTicks
                : startedUtcTicks + durationTicks * (safeClaimedCount + 1L);
            long currentStartTicks = durationTicks <= 0 ? startedUtcTicks : nextCompleteTicks - durationTicks;

            active = new ActiveRecipeState
            {
                slotIndex = slotIndex,
                recipeId = recipeId,
                startedUtcTicks = currentStartTicks,
                completesUtcTicks = nextCompleteTicks,
                queuedCount = remainingCount
            };
            return true;
        }

        private bool TryReadGold(DataChangeListener playerData, out long gold)
        {
            gold = 0;
            if (playerData == null)
            {
                return false;
            }

            try
            {
                gold = playerData.Try_GetGold();
                return true;
            }
            catch (System.Exception exception)
            {
                LogPlayerDataWarning(exception);
                return false;
            }
        }

        private bool TrySubtractGold(DataChangeListener playerData, int amount)
        {
            try
            {
                playerData.Try_SubtractGold(amount);
                return true;
            }
            catch (System.Exception exception)
            {
                LogPlayerDataWarning(exception);
                return false;
            }
        }

        private bool TryAddGold(DataChangeListener playerData, int amount)
        {
            try
            {
                playerData.Try_AddGold(amount);
                return true;
            }
            catch (System.Exception exception)
            {
                LogPlayerDataWarning(exception);
                return false;
            }
        }

        private bool TryApplyItemDeltaToPlayerData(string itemId, int countDelta)
        {
            if (!TryResolvePlayerData(out _))
            {
                return true;
            }

            if (!_stringInventoryMirrorSkippedLogged)
            {
                _stringInventoryMirrorSkippedLogged = true;
                Debug.Log("[FisherPlayerDataBridge] DataChangeListener는 현재 int itemID 인벤토리 API만 제공하므로 CSH string itemId 로컬 미러링을 건너뜁니다. 서버 큐는 PlayFabGateway.Inventory 경로를 사용합니다.");
            }

            return true;
        }

        private bool TryQueuePlayFabInventoryDelta(string itemId, int countDelta)
        {
            if (!RefreshPlayFabInventoryGatewayStatus("delta-queue", out PlayFabGateway gateway, out InventoryGateway inventory))
            {
                if (!_playFabInventoryApiMissingLogged)
                {
                    _playFabInventoryApiMissingLogged = true;
                    Debug.Log("[FisherPlayerDataBridge] 일반 itemId PlayFab Inventory 큐는 대기 중입니다. " +
                              "팀 씬에서 PlayFabGateway.Instance.Inventory가 준비되면 같은 CSH 브리지가 자동으로 큐를 사용합니다.");
                }

                return true;
            }

            int amount = countDelta < 0 ? -countDelta : countDelta;
            if (amount <= 0)
            {
                return false;
            }

            if (countDelta > 0)
            {
                inventory.Add(itemId, amount);
                SuppressInventorySnapshotPull();
                LogPlayFabInventoryQueueBridge("Add", itemId, amount);
                return true;
            }

            inventory.Remove(itemId, amount);
            SuppressInventorySnapshotPull();
            LogPlayFabInventoryQueueBridge("Remove", itemId, amount);
            return true;
        }

        private void TryBindCshRuntimeStateGateway()
        {
            PlayFabGateway gateway = PlayFabGateway.Instance;
            if (gateway == null)
            {
                UnbindCshRuntimeStateGateway();
                return;
            }

            if (ReferenceEquals(_cshRuntimeStateGateway, gateway))
            {
                return;
            }

            UnbindCshRuntimeStateGateway();
            _cshRuntimeStateGateway = gateway;
            _cshRuntimeStateGateway.CshRuntimeStateReceived += HandleCshRuntimeStateReceived;

            if (_cshRuntimeStateGateway.LastCshRuntimeState != null)
            {
                HandleCshRuntimeStateReceived(_cshRuntimeStateGateway.LastCshRuntimeState);
            }
        }

        private void UnbindCshRuntimeStateGateway()
        {
            if (_cshRuntimeStateGateway == null)
            {
                return;
            }

            _cshRuntimeStateGateway.CshRuntimeStateReceived -= HandleCshRuntimeStateReceived;
            _cshRuntimeStateGateway = null;
        }

        private void HandleCshRuntimeStateReceived(JObject runtimeState)
        {
            if (runtimeState == null || _context == null || _context.State == null)
            {
                return;
            }

            JObject root = new JObject
            {
                ["cshRuntimeState"] = runtimeState.DeepClone()
            };

            if (ApplyCloudScriptRuntimeStateResponse(root))
            {
                _context.NotifyRuntimeChanged();
            }
        }

        private static bool TryResolvePlayFabInventoryGateway(out PlayFabGateway gateway, out InventoryGateway inventory)
        {
            gateway = PlayFabGateway.Instance;
            inventory = gateway == null ? null : gateway.Inventory;
            return gateway != null && inventory != null;
        }

        private static bool TryResolvePlayFabCookingGateway(out PlayFabGateway gateway, out CookingGateway cooking)
        {
            gateway = PlayFabGateway.Instance;
            cooking = gateway == null ? null : gateway.Cooking;
            return gateway != null && cooking != null;
        }

        private bool RefreshPlayFabInventoryGatewayStatus(string source)
        {
            PlayFabGateway gateway;
            InventoryGateway inventory;
            return RefreshPlayFabInventoryGatewayStatus(source, out gateway, out inventory);
        }

        private bool RefreshPlayFabCookingGatewayStatus(string source)
        {
            PlayFabGateway gateway;
            CookingGateway cooking;
            return RefreshPlayFabCookingGatewayStatus(source, out gateway, out cooking);
        }

        private bool RefreshPlayFabInventoryGatewayStatus(string source, out PlayFabGateway gateway, out InventoryGateway inventory)
        {
            bool ready = TryResolvePlayFabInventoryGateway(out gateway, out inventory);
            if (_playFabInventoryGatewayStatusLogged && _lastPlayFabInventoryGatewayReady == ready)
            {
                return ready;
            }

            _playFabInventoryGatewayStatusLogged = true;
            _lastPlayFabInventoryGatewayReady = ready;

            if (ready)
            {
                Debug.Log("[FisherPlayerDataBridge] PlayFab Inventory bridge ready (" + source + "). " +
                          "CSH bag sell will call YWJ InventoryGateway.Sell/Flush and apply state after RefreshAllPlayerData.");
            }
            else
            {
                Debug.Log("[FisherPlayerDataBridge] PlayFab Inventory bridge waiting (" + source + "). " +
                          "When a teammate wires PlayFabGateway.Instance.Inventory in the scene, CSH bag sell auto-enables and logs sell requests.");
            }

            return ready;
        }

        private bool RefreshPlayFabCookingGatewayStatus(string source, out PlayFabGateway gateway, out CookingGateway cooking)
        {
            bool ready = TryResolvePlayFabCookingGateway(out gateway, out cooking);
            if (_playFabCookingGatewayStatusLogged && _lastPlayFabCookingGatewayReady == ready)
            {
                return ready;
            }

            _playFabCookingGatewayStatusLogged = true;
            _lastPlayFabCookingGatewayReady = ready;

            if (ready)
            {
                Debug.Log("[FisherPlayerDataBridge] PlayFab Cooking bridge ready (" + source + "). " +
                          "CSH cooking UI can request Start/Claim/Cancel/OpenSlot when the adapter server gate is enabled.");
            }
            else
            {
                Debug.Log("[FisherPlayerDataBridge] PlayFab Cooking bridge waiting (" + source + "). " +
                          "When a teammate wires PlayFabGateway.Instance.Cooking in the scene, CSH will log readiness before server cooking requests.");
            }

            return ready;
        }

        private void HandleAcceptedMutation(
            PlayFabGateway gateway,
            string operation,
            object requestArgs,
            CloudScriptMutationResponse response,
            Action onApplied,
            Action<string> onRejected)
        {
            bool isCookingMutation = IsCookingMutationOperation(operation);
            if (response != null)
            {
                string beforeActive = ActiveCookingStateForDiagnostics();
                bool applied = ApplyCloudScriptMutationResponse(
                    operation,
                    response,
                    allowDeltaFallback: true,
                    notify: true,
                    out bool cookingApplied);
                RecordCookingClaimResult(operation, response);
                bool hasCookingState = HasCookingState(response);
                LogCookingMutationDiagnostic(
                    operation,
                    "accepted applied=" + applied +
                    ", cookingApplied=" + cookingApplied +
                    ", hasCookingState=" + hasCookingState +
                    ", activeBefore=" + beforeActive +
                    ", activeAfter=" + ActiveCookingStateForDiagnostics());
                if (string.Equals(operation, "CancelCooking", StringComparison.Ordinal) &&
                    !TryValidateAcceptedCookingCancel(requestArgs, response, ref applied, out string cancelRejectMessage))
                {
                    RefreshSnapshotsInBackground(
                        gateway,
                        operation,
                        response,
                        pullDisplaySnapshots: true,
                        force: true,
                        onCompleted: () => onRejected?.Invoke(cancelRejectMessage));
                    return;
                }

                bool shouldPullDisplaySnapshots = !isCookingMutation ||
                                                  !hasCookingState ||
                                                  !applied ||
                                                  (hasCookingState && !cookingApplied);
                LogCookingMutationDiagnostic(
                    operation,
                    "accepted refreshDecision pullDisplaySnapshots=" + shouldPullDisplaySnapshots +
                    ", activeBeforeRefresh=" + ActiveCookingStateForDiagnostics());
                if (isCookingMutation && shouldPullDisplaySnapshots)
                {
                    LogCookingMutationDiagnostic(
                        operation,
                        "accepted deferredUnlockUntilCookingSnapshotRefresh activeBeforeRefresh=" +
                        ActiveCookingStateForDiagnostics());
                    RefreshSnapshotsInBackground(
                        gateway,
                        operation,
                        response,
                        pullDisplaySnapshots: true,
                        force: true,
                        onCompleted: onApplied);
                    return;
                }

                onApplied?.Invoke();
                RefreshSnapshotsInBackground(
                    gateway,
                    operation,
                    response,
                    pullDisplaySnapshots: shouldPullDisplaySnapshots);
                return;
            }

            if (gateway == null)
            {
                ApplyCloudScriptMutationResponse(operation, response, allowDeltaFallback: true);
                onApplied?.Invoke();
                return;
            }

            gateway.RefreshAllPlayerData(
                onSuccess: () =>
                {
                    if (!isCookingMutation)
                    {
                        PullDisplaySnapshotsFromPlayFabDataStore();
                    }

                    ApplyCloudScriptMutationResponse(operation, response, allowDeltaFallback: false);
                    onApplied?.Invoke();
                },
                onError: _ =>
                {
                    ApplyCloudScriptMutationResponse(operation, response, allowDeltaFallback: true);
                    onApplied?.Invoke();
                });
        }

        private void RefreshSnapshotsInBackground(
            PlayFabGateway gateway,
            string operation = "",
            CloudScriptMutationResponse response = null,
            bool pullDisplaySnapshots = true,
            bool force = false,
            Action onCompleted = null)
        {
            LogCookingMutationDiagnostic(
                operation,
                "refreshStart gateway=" + (gateway != null) +
                ", pullDisplaySnapshots=" + pullDisplaySnapshots +
                ", force=" + force +
                ", activeBefore=" + ActiveCookingStateForDiagnostics());
            if (gateway == null)
            {
                if (pullDisplaySnapshots)
                {
                    PullMutationSnapshotsFromPlayFabDataStore(operation, notify: true);
                }

                LogCookingMutationDiagnostic(
                    operation,
                    "refreshNoGateway activeAfter=" + ActiveCookingStateForDiagnostics());
                onCompleted?.Invoke();
                return;
            }

            float now = Time.unscaledTime;
            if (!force &&
                (_backgroundPlayFabSnapshotRefreshInProgress ||
                 now < _nextBackgroundPlayFabSnapshotRefreshTime))
            {
                LogCookingMutationDiagnostic(
                    operation,
                    "refreshSkipped inProgress=" + _backgroundPlayFabSnapshotRefreshInProgress +
                    ", activeAfterSkip=" + ActiveCookingStateForDiagnostics());
                onCompleted?.Invoke();
                return;
            }

            _backgroundPlayFabSnapshotRefreshInProgress = true;
            _nextBackgroundPlayFabSnapshotRefreshTime = now + BackgroundSnapshotRefreshCooldownSeconds;
            gateway.RefreshAllPlayerData(
                onSuccess: () =>
                {
                    _backgroundPlayFabSnapshotRefreshInProgress = false;
                    bool changed = false;
                    if (pullDisplaySnapshots)
                    {
                        changed |= PullMutationSnapshotsFromPlayFabDataStore(operation, notify: false, force: force);
                    }

                    if (response != null && !IsCookingMutationOperation(operation))
                    {
                        changed |= ApplyCloudScriptMutationResponse(
                            operation,
                            response,
                            allowDeltaFallback: false,
                            notify: false);
                    }

                    if (changed)
                    {
                        _context?.NotifyRuntimeChanged();
                    }

                    LogCookingMutationDiagnostic(
                        operation,
                        "refreshSuccess changed=" + changed +
                        ", activeAfter=" + ActiveCookingStateForDiagnostics());
                    onCompleted?.Invoke();
                },
                onError: _ =>
                {
                    _backgroundPlayFabSnapshotRefreshInProgress = false;
                    if (pullDisplaySnapshots)
                    {
                        PullMutationSnapshotsFromPlayFabDataStore(operation, notify: true, force: force);
                    }

                    LogCookingMutationDiagnostic(
                        operation,
                        "refreshErrorFallback activeAfter=" + ActiveCookingStateForDiagnostics());
                    onCompleted?.Invoke();
                });
        }

        private bool PullMutationSnapshotsFromPlayFabDataStore(string operation, bool notify, bool force = false)
        {
            return IsCookingMutationOperation(operation)
                ? PullCookingFromPlayFabDataStore(notify: notify, force: force)
                : PullDisplaySnapshotsFromPlayFabDataStore(notify: notify, forceInventory: force);
        }

        private void RecordCookingClaimResult(string operation, CloudScriptMutationResponse response)
        {
            if (!string.Equals(operation, "ClaimCooking", StringComparison.Ordinal) || response == null)
            {
                return;
            }

            _lastCookingClaimedCookCount = Mathf.Max(0, response.claimedCookCount);
        }

        private void HandlePlayFabMutationResult(
            PlayFabGateway gateway,
            ExecuteCloudScriptResult result,
            string operation,
            object requestArgs,
            Action onApplied,
            Action<string> onRejected)
        {
            if (!TryReadMutationSuccess(gateway, result, operation, out string rejectMessage, out CloudScriptMutationResponse response))
            {
                HandleRejectedMutation(gateway, operation, response, onRejected, rejectMessage);
                return;
            }

            HandleAcceptedMutation(gateway, operation, requestArgs, response, onApplied, onRejected);
        }

        private bool TryRequestDirectCloudScriptMutation(
            string functionName,
            object args,
            Action onApplied,
            Action<string> onRejected)
        {
            if (string.IsNullOrWhiteSpace(functionName))
            {
                return false;
            }

            PlayFabGateway gateway = PlayFabGateway.Instance;
            if (gateway == null)
            {
                LogPlayFabDataStoreWarning(functionName + " 요청을 보낼 PlayFabGateway.Instance가 없습니다.");
                return false;
            }

            return TrySendPlayFabMutationRequest(
                functionName,
                () =>
                {
                    gateway.ExecuteCloudScript(
                        functionName,
                        args,
                        result => HandlePlayFabMutationResult(gateway, result, functionName, args, onApplied, onRejected),
                        error => HandlePlayFabMutationError(gateway, error, functionName, onRejected),
                        forwardScriptErrors: true);
                    LogCookingMutationDiagnostic(
                        functionName,
                        "send args=" + JsonForDiagnostics(args) +
                        ", activeBefore=" + ActiveCookingStateForDiagnostics());
                    Debug.Log("[FisherPlayerDataBridge] Requested PlayFab CloudScript " + functionName + ": " + JsonForDiagnostics(args));
                },
                onRejected);
        }

        private static bool TrySendPlayFabMutationRequest(string operation, Action sendRequest, Action<string> onRejected)
        {
            if (!IsPlayFabClientLoggedIn())
            {
                string message = operation + " PlayFab 요청 실패: 로그인 필요";
                Debug.LogWarning("[FisherPlayerDataBridge] " + message);
                onRejected?.Invoke(message);
                return false;
            }

            try
            {
                sendRequest?.Invoke();
                return true;
            }
            catch (Exception exception)
            {
                string message = operation + " CloudScript 요청 예외: " + exception.Message;
                Debug.LogWarning("[FisherPlayerDataBridge] " + message);
                onRejected?.Invoke(message);
                return false;
            }
        }

        private static bool IsPlayFabClientLoggedIn()
        {
            try
            {
                return PlayFabSettings.staticPlayer != null && PlayFabSettings.staticPlayer.IsClientLoggedIn();
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void HandlePlayFabMutationError(
            PlayFabGateway gateway,
            PlayFabError error,
            string operation,
            Action<string> onRejected)
        {
            string message = operation + " CloudScript 호출 실패";
            if (error != null && !string.IsNullOrWhiteSpace(error.ErrorMessage))
            {
                message += ": " + error.ErrorMessage;
            }

            HandleRejectedMutation(gateway, operation, null, onRejected, message);
        }

        private void HandleRejectedMutation(
            PlayFabGateway gateway,
            string operation,
            CloudScriptMutationResponse response,
            Action<string> onRejected,
            string message)
        {
            string safeMessage = string.IsNullOrWhiteSpace(message) ? "서버 요청 실패" : message;
            bool isCookingMutation = IsCookingMutationOperation(operation);
            bool hasCookingState = HasCookingState(response);
            bool applied = false;
            bool cookingApplied = false;
            string activeBeforeApply = ActiveCookingStateForDiagnostics();
            if (response != null)
            {
                applied = ApplyCloudScriptMutationResponse(
                    operation,
                    response,
                    allowDeltaFallback: false,
                    notify: true,
                    out cookingApplied);
            }

            LogCookingMutationDiagnostic(
                operation,
                "rejected isCookingMutation=" + isCookingMutation +
                ", hasCookingState=" + hasCookingState +
                ", applied=" + applied +
                ", cookingApplied=" + cookingApplied +
                ", message=" + safeMessage +
                ", activeBeforeApply=" + activeBeforeApply +
                ", activeAfterApply=" + ActiveCookingStateForDiagnostics());

            if (!isCookingMutation)
            {
                PullDisplaySnapshotsFromPlayFabDataStore();
            }

            if (isCookingMutation && hasCookingState)
            {
                if (!cookingApplied)
                {
                    LogCookingMutationDiagnostic(
                        operation,
                        "rejectedCookingStateApplyFailed willForceRefresh=true, activeBeforeRefresh=" +
                        ActiveCookingStateForDiagnostics());
                    RefreshSnapshotsInBackground(
                        gateway,
                        operation,
                        response: null,
                        pullDisplaySnapshots: true,
                        force: true,
                        onCompleted: () => onRejected?.Invoke(safeMessage));
                    return;
                }

                LogCookingMutationDiagnostic(
                    operation,
                    "rejectedCompleteWithCookingState willForceRefresh=false, activeFinal=" + ActiveCookingStateForDiagnostics());
                onRejected?.Invoke(safeMessage);
                return;
            }

            if (isCookingMutation)
            {
                LogCookingMutationDiagnostic(
                    operation,
                    "rejectedNoCookingState willForceRefresh=true, activeBeforeRefresh=" + ActiveCookingStateForDiagnostics());
                RefreshSnapshotsInBackground(
                    gateway,
                    operation,
                    response: null,
                    pullDisplaySnapshots: true,
                    force: true,
                    onCompleted: () => onRejected?.Invoke(safeMessage));
                return;
            }

            RefreshSnapshotsInBackground(
                gateway,
                operation,
                response: null,
                pullDisplaySnapshots: !isCookingMutation);
            onRejected?.Invoke(safeMessage);
        }

        private static bool IsCookingMutationOperation(string operation)
        {
            switch (operation)
            {
                case "StartCooking":
                case "ClaimCooking":
                case "CancelCooking":
                case "OpenCookingSlot":
                case CookingSpeedupFunctionName:
                    return true;
                default:
                    return false;
            }
        }

        private static bool HasCookingState(CloudScriptMutationResponse response)
        {
            if (response == null || string.IsNullOrWhiteSpace(response.rawJson))
            {
                return false;
            }

            try
            {
                JObject root = ParseMutationJson(response.rawJson);
                if (root["cookingData"] != null ||
                    root["cookSlots"] != null ||
                    root["slot"] != null)
                {
                    return true;
                }

                return root["data"] is JObject data &&
                       (data["cookingData"] != null ||
                        data["cookSlots"] != null ||
                        data["slot"] != null);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool TryValidateAcceptedCookingCancel(
            object requestArgs,
            CloudScriptMutationResponse response,
            ref bool applied,
            out string rejectMessage)
        {
            rejectMessage = string.Empty;
            if (!TryReadCookingRequestIdentity(requestArgs, out CookingRequestIdentity identity))
            {
                return true;
            }

            if (TryGetCookingSlotsObject(response, out JObject cookSlotsObject))
            {
                if (CookingSlotsContainJob(cookSlotsObject, identity))
                {
                    rejectMessage = "CancelCooking 성공 응답에 요청한 요리 job이 아직 남아 있습니다.";
                    Debug.LogWarning("[FisherPlayerDataBridge] " + rejectMessage);
                    return false;
                }

                if (!applied)
                {
                    applied |= ClearRuntimeCookingJob(identity);
                }

                return true;
            }

            if (!applied)
            {
                applied |= ClearRuntimeCookingJob(identity);
            }

            return true;
        }

        private bool ClearRuntimeCookingJob(CookingRequestIdentity identity)
        {
            if (_context == null || _context.CookingService == null)
            {
                return false;
            }

            ServiceResult result = _context.CookingService.ClearServerCookingJob(
                identity.slotIndex,
                identity.recipeId,
                identity.startedUtcTicks);
            if (result == null || !result.Success)
            {
                LogPlayFabDataStoreWarning("CancelCooking 로컬 job 정리 실패: " +
                                           (result == null ? "null result" : result.MessageKey));
                return false;
            }

            return true;
        }

        private static bool TryReadCookingRequestIdentity(object requestArgs, out CookingRequestIdentity identity)
        {
            identity = default;
            if (requestArgs == null)
            {
                return false;
            }

            try
            {
                JObject args = JObject.FromObject(requestArgs);
                identity.slotIndex = ReadInt(args["slotIndex"], -1);
                identity.recipeId = ReadString(args["recipeId"]);
                identity.startedAtUtc = ReadString(args["activeStartedAtUtc"]);
                if (TryReadUtcTicks(identity.startedAtUtc, out long startedUtcTicks))
                {
                    identity.startedUtcTicks = startedUtcTicks;
                }

                return identity.slotIndex >= 0 ||
                       !string.IsNullOrWhiteSpace(identity.recipeId) ||
                       identity.startedUtcTicks > 0L;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool TryGetCookingSlotsObject(CloudScriptMutationResponse response, out JObject cookSlotsObject)
        {
            cookSlotsObject = null;
            if (response == null || string.IsNullOrWhiteSpace(response.rawJson))
            {
                return false;
            }

            try
            {
                JObject root = ParseMutationJson(response.rawJson);
                JToken cookingToken = root["cookingData"] ?? root["cookSlot"];
                if (cookingToken == null && root["data"] is JObject data)
                {
                    cookingToken = data["cookingData"] ?? data["cookSlot"];
                }

                if (cookingToken is JObject cookingObject &&
                    cookingObject["cookSlots"] is JObject slots)
                {
                    cookSlotsObject = slots;
                    return true;
                }

                if (root["cookSlots"] is JObject directSlots)
                {
                    cookSlotsObject = directSlots;
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }

            return false;
        }

        private static bool CookingSlotsContainJob(JObject cookSlotsObject, CookingRequestIdentity identity)
        {
            if (cookSlotsObject == null)
            {
                return false;
            }

            foreach (JProperty slotProperty in cookSlotsObject.Properties())
            {
                if (!(slotProperty.Value is JObject slotObject) ||
                    !(slotObject["job"] is JObject jobObject))
                {
                    continue;
                }

                int slotIndex = -1;
                int.TryParse(slotProperty.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out slotIndex);
                if (CookingJobMatchesIdentity(slotIndex, jobObject, identity))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CookingJobMatchesIdentity(int slotIndex, JObject jobObject, CookingRequestIdentity identity)
        {
            if (jobObject == null)
            {
                return false;
            }

            bool hasStableJobIdentity = !string.IsNullOrWhiteSpace(identity.recipeId) ||
                                        identity.startedUtcTicks > 0L;
            if (!hasStableJobIdentity &&
                identity.slotIndex >= 0 &&
                slotIndex != identity.slotIndex)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(identity.recipeId) &&
                !string.Equals(ReadString(jobObject["recipeId"]), identity.recipeId, StringComparison.Ordinal))
            {
                return false;
            }

            if (identity.startedUtcTicks > 0L)
            {
                return TryReadActiveCookingStartTicks(jobObject, out long activeStartedTicks) &&
                       activeStartedTicks == identity.startedUtcTicks;
            }

            return true;
        }

        private static bool TryReadActiveCookingStartTicks(JObject jobObject, out long ticks)
        {
            ticks = 0L;
            if (jobObject == null ||
                !TryReadUtcTicks(ReadString(jobObject["startedAtUtc"]), out long startedUtcTicks))
            {
                return false;
            }

            int durationSec = ReadInt(jobObject["durationSec"], 0);
            int claimedCount = ReadInt(jobObject["claimedCount"], 0);
            long offsetTicks = durationSec > 0 && claimedCount > 0
                ? TimeSpan.FromSeconds(durationSec).Ticks * Math.Max(0, claimedCount)
                : 0L;
            ticks = startedUtcTicks + offsetTicks;
            return true;
        }

        private static bool TryReadMutationSuccess(
            PlayFabGateway gateway,
            ExecuteCloudScriptResult result,
            string operation,
            out string rejectMessage,
            out CloudScriptMutationResponse response)
        {
            rejectMessage = string.Empty;
            response = null;
            if (result == null)
            {
                rejectMessage = operation + " 응답이 비어 있습니다.";
                return false;
            }

            if (result.Error != null)
            {
                rejectMessage = operation + " CloudScript 오류";
                if (!string.IsNullOrWhiteSpace(result.Error.Message))
                {
                    rejectMessage += ": " + result.Error.Message;
                }

                return false;
            }

            if (result.FunctionResult == null)
            {
                rejectMessage = operation + " FunctionResult가 비어 있습니다.";
                return false;
            }

            string json = gateway == null
                ? result.FunctionResult.ToString()
                : gateway.SerializeFunctionResult(result);
            if (string.IsNullOrWhiteSpace(json))
            {
                rejectMessage = operation + " FunctionResult 직렬화 실패";
                return false;
            }

            LogCookingMutationDiagnostic(
                operation,
                "FunctionResult raw=" + TruncateForDiagnostics(json));
            try
            {
                response = JsonUtility.FromJson<CloudScriptMutationResponse>(json);
                if (response != null)
                {
                    response.rawJson = json;
                }
            }
            catch (Exception exception)
            {
                rejectMessage = operation + " FunctionResult 파싱 실패: " + exception.Message;
                return false;
            }

            LogCookingMutationDiagnostic(
                operation,
                "response success=" + (response != null && response.success) +
                ", duplicate=" + (response != null && response.duplicate) +
                ", actionType=" + (response == null ? string.Empty : response.actionType) +
                ", error=" + (response == null ? string.Empty : response.error) +
                ", errorCode=" + (response == null ? string.Empty : response.errorCode) +
                ", message=" + (response == null ? string.Empty : response.message) +
                ", serverNowUtc=" + (response == null ? string.Empty : response.serverNowUtc) +
                ", mutationAppliedAtUtc=" + (response == null ? string.Empty : response.mutationAppliedAtUtc) +
                ", cloudScriptBuildId=" + (response == null ? string.Empty : response.cloudScriptBuildId) +
                ", cshBuildId=" + (response == null ? string.Empty : response.cshBuildId) +
                ", hasCookingState=" + HasCookingState(response));
            if (response != null && response.success)
            {
                return true;
            }

            string error = response == null ? string.Empty : response.error;
            if (string.IsNullOrWhiteSpace(error) && response != null)
            {
                error = response.errorCode;
            }

            if (string.IsNullOrWhiteSpace(error) && response != null)
            {
                error = response.message;
            }
            else if (response != null &&
                     !string.IsNullOrWhiteSpace(response.message) &&
                     !string.Equals(error, response.message, StringComparison.Ordinal))
            {
                error += " / " + response.message;
            }

            rejectMessage = string.IsNullOrWhiteSpace(error)
                ? operation + " 서버 요청 실패"
                : operation + " 서버 요청 실패: " + error;
            return false;
        }

        private bool ApplyCloudScriptMutationResponse(
            string operation,
            CloudScriptMutationResponse response,
            bool allowDeltaFallback,
            bool notify = true)
        {
            return ApplyCloudScriptMutationResponse(
                operation,
                response,
                allowDeltaFallback,
                notify,
                out _);
        }

        private bool ApplyCloudScriptMutationResponse(
            string operation,
            CloudScriptMutationResponse response,
            bool allowDeltaFallback,
            bool notify,
            out bool cookingApplied)
        {
            cookingApplied = false;
            if (response == null || _context == null || _context.State == null)
            {
                return false;
            }

            RecordCookingMutationClock(operation, response);

            JObject root = null;
            if (!string.IsNullOrWhiteSpace(response.rawJson))
            {
                try
                {
                    root = ParseMutationJson(response.rawJson);
                }
                catch (Exception exception)
                {
                    LogPlayFabDataStoreWarning(operation + " FunctionResult JObject 파싱 실패: " + exception.Message);
                }
            }

            bool changed = false;
            if (root != null)
            {
                bool inventoryApplied = ApplyCloudScriptInventoryResponse(operation, root, allowDeltaFallback);
                changed |= inventoryApplied;
                cookingApplied = ApplyCloudScriptCookingResponse(operation, root);
                changed |= cookingApplied;
                changed |= ApplyCloudScriptCurrencyResponse(root, allowDeltaFallback);
                changed |= ApplyCloudScriptRuntimeStateResponse(root);
                changed |= ApplyCloudScriptHiddenCashResponse(root);
                if (IsCookingMutationOperation(operation))
                {
                    if (inventoryApplied)
                    {
                        SuppressInventorySnapshotPull();
                    }

                    if (cookingApplied)
                    {
                        SuppressCookingSnapshotPull();
                    }
                }
            }

            if (string.Equals(operation, BagCapacityExpansionFunctionName, StringComparison.Ordinal))
            {
                return ApplyCloudScriptBagCapacityResponse(response, changed, notify);
            }

            if (string.Equals(operation, CollectionRewardClaimFunctionName, StringComparison.Ordinal))
            {
                changed |= ApplyCloudScriptCollectionRewardResponse(response);
            }

            if (changed && notify)
            {
                _context.NotifyRuntimeChanged();
            }

            return changed;
        }

        private static JObject ParseMutationJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            using (StringReader stringReader = new StringReader(json))
            using (JsonTextReader jsonReader = new JsonTextReader(stringReader))
            {
                jsonReader.DateParseHandling = DateParseHandling.None;
                return JObject.Load(jsonReader);
            }
        }

        private void RecordCookingMutationClock(string operation, CloudScriptMutationResponse response)
        {
            if (!IsCookingMutationOperation(operation) || response == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(response.actionType) &&
                !string.Equals(response.actionType, operation, StringComparison.Ordinal))
            {
                LogPlayFabDataStoreWarning(
                    operation + " actionType mismatch: response=" + response.actionType);
            }

            if (TryReadUtcTicks(response.serverNowUtc, out long serverNowTicks))
            {
                if (_lastCookingServerNowUtcTicks > 0L &&
                    serverNowTicks < _lastCookingServerNowUtcTicks)
                {
                    LogPlayFabDataStoreWarning(
                        operation + " stale cooking server clock warning: serverNowUtc=" +
                        response.serverNowUtc +
                        ", lastServerNowUtc=" + _lastCookingServerNowUtc);
                }

                _lastCookingServerNowUtc = response.serverNowUtc;
                _lastCookingServerNowUtcTicks = serverNowTicks;
            }

            if (!TryReadUtcTicks(response.mutationAppliedAtUtc, out long appliedTicks))
            {
                return;
            }

            if (_lastCookingMutationAppliedUtcTicks > 0L &&
                appliedTicks < _lastCookingMutationAppliedUtcTicks)
            {
                LogPlayFabDataStoreWarning(
                    operation + " stale cooking mutation response warning: mutationAppliedAtUtc=" +
                    response.mutationAppliedAtUtc +
                    ", lastMutationAppliedAtUtc=" + _lastCookingMutationAppliedAtUtc);
                return;
            }

            _lastCookingMutationAppliedAtUtc = response.mutationAppliedAtUtc;
            _lastCookingMutationAppliedUtcTicks = appliedTicks;
        }

        private bool ApplyCloudScriptBagCapacityResponse(CloudScriptMutationResponse response, bool changed, bool notify)
        {
            if (response == null || _context == null || _context.State == null)
            {
                return false;
            }

            if (response.capacityAfter > 0 && _context.State.bagCapacity != response.capacityAfter)
            {
                _context.State.bagCapacity = response.capacityAfter;
                changed = true;
            }

            if (response.bagCapacityLevel >= 0 && _context.State.bagCapacityLevel != response.bagCapacityLevel)
            {
                _context.State.bagCapacityLevel = response.bagCapacityLevel;
                changed = true;
            }

            if (changed)
            {
                Debug.Log("[FisherPlayerDataBridge] Applied CSH bag capacity from CloudScript: capacity=" +
                          _context.State.bagCapacity + ", level=" + _context.State.bagCapacityLevel);
            }

            if (changed && notify)
            {
                _context.NotifyRuntimeChanged();
            }

            return changed;
        }

        private bool ApplyCloudScriptCollectionRewardResponse(CloudScriptMutationResponse response)
        {
            if (response == null || _context == null || _context.State == null)
            {
                return false;
            }

            string claimId = response.claimId;
            if (string.IsNullOrWhiteSpace(claimId) &&
                !string.IsNullOrWhiteSpace(response.rewardId) &&
                _context.BuildResult != null &&
                _context.BuildResult.Catalog != null &&
                _context.BuildResult.Catalog.TryGetCollectionReward(response.rewardId, out CollectionRewardDefinition reward) &&
                reward != null)
            {
                claimId = reward.ClaimId;
            }

            if (string.IsNullOrWhiteSpace(claimId) || !_context.State.claimedRewardIds.Add(claimId))
            {
                return false;
            }

            Debug.Log("[FisherPlayerDataBridge] Applied CSH collection reward claim from CloudScript: " + claimId);
            return true;
        }

        private bool ApplyCloudScriptInventoryResponse(string operation, JObject root, bool allowDeltaFallback)
        {
            if (_context == null || _context.InventoryService == null || root == null)
            {
                return false;
            }

            bool changed = false;
            JObject dataObject = ReadMutationDataObject(root);
            JToken inventoryToken = ReadMutationToken(root, dataObject, "inventory");

            if (inventoryToken is JObject inventoryObject)
            {
                string singleInventoryKey = ReadMutationString(root, dataObject, "inventoryKey");
                changed |= ApplyPartialInventorySnapshot(inventoryObject, singleInventoryKey);
            }

            if (!changed && allowDeltaFallback)
            {
                changed |= ApplyCloudScriptInventoryDeltaFallback(operation, root);
            }

            return changed;
        }

        private bool ApplyCloudScriptInventoryDeltaFallback(string operation, JObject root)
        {
            if (_context == null || _context.InventoryService == null || root == null)
            {
                return false;
            }

            bool changed = false;
            if (string.Equals(operation, "StartCooking", StringComparison.Ordinal) && root["consumed"] is JArray consumed)
            {
                changed |= ApplyInventoryDeltaList(consumed, -1, useReturnPath: false);
            }
            else if (string.Equals(operation, "CancelCooking", StringComparison.Ordinal) && root["returned"] is JArray returned)
            {
                changed |= ApplyInventoryDeltaList(returned, 1, useReturnPath: true);
            }
            else if (string.Equals(operation, "ClaimCooking", StringComparison.Ordinal))
            {
                string itemId = ReadString(root["outputItemId"]);
                int amount = ReadInt(root["addedItemCount"], 0);
                if (!string.IsNullOrWhiteSpace(itemId) && amount > 0)
                {
                    ServiceResult result = _context.InventoryService.TryAddItem(itemId, amount);
                    changed |= result != null && result.Success;
                }
            }
            else if (string.Equals(operation, ShopPurchaseFunctionName, StringComparison.Ordinal))
            {
                string itemId = ReadString(root["rewardItemId"]);
                int amount = ReadInt(root["rewardAmount"], 0);
                if (!string.IsNullOrWhiteSpace(itemId) && amount > 0)
                {
                    ServiceResult result = _context.InventoryService.TryAddItem(itemId, amount);
                    changed |= result != null && result.Success;
                }
            }
            else if (string.Equals(operation, BoxUseFunctionName, StringComparison.Ordinal))
            {
                JObject dataObject = ReadMutationDataObject(root);
                string boxItemId = ReadMutationString(root, dataObject, "boxItemId");
                if (string.IsNullOrWhiteSpace(boxItemId))
                {
                    boxItemId = ReadMutationString(root, dataObject, "itemId");
                }

                int boxCount = Math.Max(1, ReadMutationInt(root, dataObject, "count", 1));
                if (!string.IsNullOrWhiteSpace(boxItemId))
                {
                    ServiceResult consume = _context.InventoryService.TryConsumeItem(boxItemId, boxCount);
                    changed |= consume != null && consume.Success;
                }

                JArray rewards = ReadMutationToken(root, dataObject, "rewards") as JArray;
                if (rewards == null)
                {
                    rewards = ReadMutationToken(root, dataObject, "grantedRewards") as JArray;
                }

                changed |= ApplyInventoryDeltaList(rewards, 1, useReturnPath: false);
            }

            return changed;
        }

        private bool ApplyInventoryDeltaList(JArray items, int sign, bool useReturnPath)
        {
            if (_context == null || _context.InventoryService == null || items == null)
            {
                return false;
            }

            bool changed = false;
            for (int i = 0; i < items.Count; i++)
            {
                JObject item = items[i] as JObject;
                if (item == null)
                {
                    continue;
                }

                string itemId = ReadString(item["itemId"]);
                int amount = ReadInt(item["amount"], 0);
                if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
                {
                    continue;
                }

                ServiceResult result = sign < 0
                    ? _context.InventoryService.TryConsumeItem(itemId, amount)
                    : useReturnPath
                        ? _context.InventoryService.TryReturnConsumedItem(itemId, amount)
                        : _context.InventoryService.TryAddItem(itemId, amount);
                changed |= result != null && result.Success;
            }

            return changed;
        }

        private bool ApplyPartialInventorySnapshot(JObject inventoryObject, string singleInventoryKey)
        {
            if (inventoryObject == null || _context == null || _context.InventoryService == null)
            {
                return false;
            }

            Dictionary<string, int> merged = BuildCurrentInventoryCountSnapshot();
            bool changed = false;
            if (IsInventoryGroupObject(inventoryObject))
            {
                foreach (JProperty group in inventoryObject.Properties())
                {
                    if (group.Value is JObject itemMap)
                    {
                        if (TryNormalizeInventoryKey(group.Name, out string groupInventoryKey))
                        {
                            changed |= RemoveInventoryGroupSnapshot(merged, groupInventoryKey);
                        }

                        changed |= MergeInventoryItemMap(merged, itemMap);
                    }
                }
            }
            else
            {
                if (TryNormalizeInventoryKey(singleInventoryKey, out string inventoryKey))
                {
                    changed |= RemoveInventoryGroupSnapshot(merged, inventoryKey);
                }

                changed |= MergeInventoryItemMap(merged, inventoryObject);
            }

            if (!changed)
            {
                return false;
            }

            ServiceResult result = _context.InventoryService.ReplaceStackedInventorySnapshot(merged);
            if (result == null || !result.Success)
            {
                LogPlayFabDataStoreWarning("CloudScript inventory snapshot 반영 실패: " +
                                           (result == null ? "null result" : result.MessageKey) +
                                           (string.IsNullOrWhiteSpace(singleInventoryKey) ? string.Empty : ", inventoryKey=" + singleInventoryKey));
                return false;
            }

            return true;
        }

        private Dictionary<string, int> BuildCurrentInventoryCountSnapshot()
        {
            Dictionary<string, int> snapshot = new Dictionary<string, int>(StringComparer.Ordinal);
            if (_context == null || _context.InventoryService == null)
            {
                return snapshot;
            }

            IReadOnlyList<InventoryEntry> entries = _context.InventoryService.GetInventorySnapshot();
            for (int i = 0; i < entries.Count; i++)
            {
                InventoryEntry entry = entries[i];
                if (entry == null ||
                    string.IsNullOrWhiteSpace(entry.itemId) ||
                    entry.count <= 0 ||
                    !string.IsNullOrEmpty(entry.instanceId) ||
                    entry.levelIndex >= 0)
                {
                    continue;
                }

                if (!snapshot.TryGetValue(entry.itemId, out int count))
                {
                    count = 0;
                }

                long next = (long)count + entry.count;
                snapshot[entry.itemId] = next > int.MaxValue ? int.MaxValue : (int)next;
            }

            return snapshot;
        }

        private static bool IsInventoryGroupObject(JObject inventoryObject)
        {
            foreach (JProperty property in inventoryObject.Properties())
            {
                if (property.Value is JObject &&
                    (property.Name == FishInventoryKey ||
                     property.Name == FoodInventoryKey ||
                     property.Name == IngredientInventoryKey ||
                     property.Name == OddmentInventoryKey))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryNormalizeInventoryKey(string rawInventoryKey, out string inventoryKey)
        {
            inventoryKey = string.Empty;
            if (string.IsNullOrWhiteSpace(rawInventoryKey))
            {
                return false;
            }

            if (string.Equals(rawInventoryKey, FishInventoryKey, StringComparison.Ordinal))
            {
                inventoryKey = FishInventoryKey;
                return true;
            }

            if (string.Equals(rawInventoryKey, FoodInventoryKey, StringComparison.Ordinal))
            {
                inventoryKey = FoodInventoryKey;
                return true;
            }

            if (string.Equals(rawInventoryKey, IngredientInventoryKey, StringComparison.Ordinal))
            {
                inventoryKey = IngredientInventoryKey;
                return true;
            }

            if (string.Equals(rawInventoryKey, OddmentInventoryKey, StringComparison.Ordinal))
            {
                inventoryKey = OddmentInventoryKey;
                return true;
            }

            return false;
        }

        private static bool RemoveInventoryGroupSnapshot(Dictionary<string, int> merged, string inventoryKey)
        {
            if (merged == null || string.IsNullOrWhiteSpace(inventoryKey))
            {
                return false;
            }

            List<string> keysToRemove = null;
            foreach (KeyValuePair<string, int> pair in merged)
            {
                if (TryGetInventoryKeyByItemId(pair.Key, out string itemInventoryKey) &&
                    string.Equals(itemInventoryKey, inventoryKey, StringComparison.Ordinal))
                {
                    keysToRemove ??= new List<string>();
                    keysToRemove.Add(pair.Key);
                }
            }

            if (keysToRemove == null)
            {
                return false;
            }

            bool changed = false;
            for (int i = 0; i < keysToRemove.Count; i++)
            {
                changed |= merged.Remove(keysToRemove[i]);
            }

            return changed;
        }

        private static bool MergeInventoryItemMap(Dictionary<string, int> merged, JObject itemMap)
        {
            if (merged == null || itemMap == null)
            {
                return false;
            }

            bool changed = false;
            foreach (JProperty item in itemMap.Properties())
            {
                int count = ReadInt(item.Value, 0);
                if (count <= 0)
                {
                    changed |= merged.Remove(item.Name);
                    continue;
                }

                if (!merged.TryGetValue(item.Name, out int previous) || previous != count)
                {
                    merged[item.Name] = count;
                    changed = true;
                }
            }

            return changed;
        }

        private bool ApplyCloudScriptCookingResponse(string operation, JObject root)
        {
            if (_context == null || _context.CookingService == null || root == null)
            {
                LogCookingMutationDiagnostic(operation, "applyCooking skipped context-or-root-null");
                return false;
            }

            string activeBefore = ActiveCookingStateForDiagnostics();
            JObject dataObject = ReadMutationDataObject(root);
            JToken cookingToken = ReadMutationToken(root, dataObject, "cookingData", "cookSlot");

            if (cookingToken is JObject cookingObject &&
                cookingObject["cookSlots"] is JObject cookSlotsObject)
            {
                bool applied = ApplyCookingSlotsSnapshot(cookSlotsObject);
                LogCookingMutationDiagnostic(
                    operation,
                    "applyCooking branch=fullCookingData applied=" + applied +
                    ", activeBefore=" + activeBefore +
                    ", activeAfter=" + ActiveCookingStateForDiagnostics());
                return applied;
            }

            JObject slotObject = ReadMutationToken(root, dataObject, "slot") as JObject;
            if (slotObject != null)
            {
                int slotIndex = ReadMutationInt(root, dataObject, "slotIndex", -1);
                if (slotIndex < 0)
                {
                    slotIndex = ReadInt(slotObject["slotIndex"], -1);
                }

                if (slotIndex >= 0)
                {
                    bool applied = ApplySingleCookingSlot(slotIndex, slotObject);
                    LogCookingMutationDiagnostic(
                        operation,
                        "applyCooking branch=singleSlot slot=" + slotIndex +
                        ", applied=" + applied +
                        ", activeBefore=" + activeBefore +
                        ", activeAfter=" + ActiveCookingStateForDiagnostics());
                    return applied;
                }
            }

            if (string.Equals(operation, "CancelCooking", StringComparison.Ordinal))
            {
                int slotIndex = ReadMutationInt(root, dataObject, "slotIndex", -1);
                if (slotIndex >= 0)
                {
                    JObject clearedSlot = new JObject
                    {
                        ["isOpened"] = true,
                        ["job"] = null
                    };
                    bool applied = ApplySingleCookingSlot(slotIndex, clearedSlot);
                    LogCookingMutationDiagnostic(
                        operation,
                        "applyCooking branch=cancelFallback slot=" + slotIndex +
                        ", applied=" + applied +
                        ", activeBefore=" + activeBefore +
                        ", activeAfter=" + ActiveCookingStateForDiagnostics());
                    return applied;
                }
            }

            LogCookingMutationDiagnostic(
                operation,
                "applyCooking branch=none applied=false, activeBefore=" + activeBefore +
                ", activeAfter=" + ActiveCookingStateForDiagnostics());
            return false;
        }

        private bool ApplyCookingSlotsSnapshot(JObject cookSlotsObject)
        {
            if (cookSlotsObject == null || _context == null || _context.CookingService == null)
            {
                return false;
            }

            List<ActiveRecipeState> activeRecipes = new List<ActiveRecipeState>();
            int openedSlotCount = CookingService.FixedCookingSlotCount;
            int expectedActiveJobCount = 0;
            foreach (JProperty property in cookSlotsObject.Properties())
            {
                if (!(property.Value is JObject slotObject))
                {
                    continue;
                }

                if (!int.TryParse(property.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedSlotIndex) ||
                    parsedSlotIndex < 0 ||
                    parsedSlotIndex >= CookingService.FixedCookingSlotCount)
                {
                    if (slotObject["job"] is JObject hiddenJobObject && IsActiveCookingJobObject(hiddenJobObject))
                    {
                        LogPlayFabDataStoreWarning(
                            "CloudScript cookingData에 fixed-3 범위 밖 active job이 있어 로컬 표시에 반영하지 않았습니다. slot=" +
                            property.Name +
                            ", recipeId=" + ReadString(hiddenJobObject["recipeId"]));
                    }

                    continue;
                }

                if (!(slotObject["job"] is JObject jobObject))
                {
                    continue;
                }

                if (!IsActiveCookingJobObject(jobObject))
                {
                    continue;
                }

                expectedActiveJobCount++;
                if (TryBuildActiveRecipeFromServerSlot(property.Name, jobObject, out ActiveRecipeState active))
                {
                    activeRecipes.Add(active);
                    continue;
                }

                LogPlayFabDataStoreWarning(
                    "CloudScript cookingData job 변환 실패: slot=" + property.Name +
                    ", recipeId=" + ReadString(jobObject["recipeId"]) +
                    ", totalCount=" + ReadInt(jobObject["totalCount"], 0) +
                    ", claimedCount=" + ReadInt(jobObject["claimedCount"], 0) +
                    ", durationSec=" + ReadInt(jobObject["durationSec"], 0) +
                    ", startedAtUtc=" + ReadString(jobObject["startedAtUtc"]));
            }

            if (expectedActiveJobCount > 0 && activeRecipes.Count == 0)
            {
                LogPlayFabDataStoreWarning(
                    "CloudScript cookingData에 진행 job이 있지만 로컬 active 변환 결과가 비었습니다. " +
                    "expectedJobs=" + expectedActiveJobCount + ", openedSlots=" + openedSlotCount);
                return false;
            }

            ServiceResult result = _context.CookingService.ReplaceServerCookingSnapshot(activeRecipes, openedSlotCount);
            if (result == null || !result.Success)
            {
                LogPlayFabDataStoreWarning("CloudScript cookingData 반영 실패: " +
                                           (result == null ? "null result" : result.MessageKey));
                return false;
            }

            if (expectedActiveJobCount > 0 && _context.CookingService.ActiveCookingSlotCount == 0)
            {
                LogPlayFabDataStoreWarning(
                    "CloudScript cookingData 적용 후 active 요리가 비었습니다. " +
                    "expectedJobs=" + expectedActiveJobCount +
                    ", parsedJobs=" + activeRecipes.Count +
                    ", openedSlots=" + openedSlotCount);
                return false;
            }

            return true;
        }

        private static bool IsActiveCookingJobObject(JObject jobObject)
        {
            return jobObject != null &&
                   !string.IsNullOrWhiteSpace(ReadString(jobObject["recipeId"])) &&
                   ReadInt(jobObject["totalCount"], 0) > ReadInt(jobObject["claimedCount"], 0);
        }

        private bool ApplySingleCookingSlot(int slotIndex, JObject slotObject)
        {
            if (slotIndex < 0 || slotObject == null || _context == null || _context.CookingService == null)
            {
                return false;
            }

            List<ActiveRecipeState> activeRecipes = new List<ActiveRecipeState>();
            IReadOnlyList<ActiveRecipeState> current = _context.CookingService.ActiveRecipeStates;
            for (int i = 0; i < current.Count; i++)
            {
                ActiveRecipeState active = current[i];
                if (active != null && active.slotIndex != slotIndex)
                {
                    activeRecipes.Add(active);
                }
            }

            if (slotObject["job"] is JObject jobObject &&
                TryBuildActiveRecipeFromServerSlot(slotIndex.ToString(CultureInfo.InvariantCulture), jobObject, out ActiveRecipeState nextActive))
            {
                activeRecipes.Add(nextActive);
            }

            int openedSlotCount = CookingService.FixedCookingSlotCount;

            ServiceResult result = _context.CookingService.ReplaceServerCookingSnapshot(activeRecipes, openedSlotCount);
            if (result == null || !result.Success)
            {
                LogPlayFabDataStoreWarning("CloudScript cooking slot 반영 실패: " +
                                           (result == null ? "null result" : result.MessageKey) +
                                           ", slot=" + slotIndex);
                return false;
            }

            return true;
        }

        private bool ApplyCloudScriptCurrencyResponse(JObject root, bool allowDeltaFallback)
        {
            if (_context == null || _context.State == null || root == null)
            {
                return false;
            }

            bool changed = false;
            JToken currencyToken = root["currency"];
            if (currencyToken == null && root["data"] is JObject data)
            {
                currencyToken = data["currency"];
            }

            if (currencyToken is JObject currency)
            {
                changed |= ApplyCurrencyValue("GD", ReadNullableLong(currency["gold"]));
                changed |= ApplyCurrencyValue("PP", ReadNullableLong(currency["prismPearl"]));
                changed |= ApplyCurrencyValue("PC", ReadNullableLong(currency["pirateCoin"]));
            }

            long? currencyAfter = ReadNullableLong(root["currencyAfter"]);
            if (currencyAfter.HasValue)
            {
                changed |= ApplyCurrencyValue(ReadString(root["spentCurrency"]), currencyAfter);
            }
            else if (allowDeltaFallback)
            {
                long? gainedGold = ReadNullableLong(root["gainedGold"]);
                if (gainedGold.HasValue && gainedGold.Value != 0)
                {
                    changed |= FisherCurrencyContract.TryAddBalance(
                        _context.State,
                        "GD",
                        gainedGold.Value,
                        out _);
                }
            }

            return changed;
        }

        private bool ApplyCloudScriptRuntimeStateResponse(JObject root)
        {
            if (_context == null || _context.State == null || root == null)
            {
                return false;
            }

            JToken runtimeToken = root["cshRuntimeState"];
            if (runtimeToken == null && root["data"] is JObject data)
            {
                runtimeToken = data["cshRuntimeState"];
            }

            if (!(runtimeToken is JObject runtimeState))
            {
                return false;
            }

            bool changed = false;
            PlayerRuntimeState state = _context.State;
            changed |= ApplyRuntimeLong(runtimeState, "cash", ref state.cash);
            changed |= ApplyRuntimeInt(runtimeState, "bagCapacity", ref state.bagCapacity);
            changed |= ApplyRuntimeInt(runtimeState, "bagCapacityLevel", ref state.bagCapacityLevel);
            changed |= ApplyRuntimeInt(runtimeState, "cookingSlotLimit", ref state.cookingSlotLimit);
            changed |= ApplyRuntimeInt(runtimeState, "cookingSlotLevel", ref state.cookingSlotLevel);
            changed |= ApplyRuntimeInt(runtimeState, "currentStage", ref state.currentStage);
            changed |= ApplyRuntimeInt(runtimeState, "farthestStage", ref state.farthestStage);
            changed |= ReplaceRuntimeIntMap(
                runtimeState["itemAcquisitionCounts"],
                state.itemAcquisitionCounts);
            changed |= ReplaceRuntimeStringSet(
                runtimeState["discoveredCollectionItemIds"],
                state.discoveredCollectionItemIds);
            changed |= ReplaceRuntimeStringSet(
                runtimeState["claimedCollectionRewards"],
                state.claimedRewardIds);

            return changed;
        }

        private static bool ApplyRuntimeLong(JObject runtimeState, string key, ref long target)
        {
            long? value = runtimeState == null ? null : ReadNullableLong(runtimeState[key]);
            if (!value.HasValue || target == value.Value)
            {
                return false;
            }

            target = value.Value;
            return true;
        }

        private static bool ApplyRuntimeInt(JObject runtimeState, string key, ref int target)
        {
            long? value = runtimeState == null ? null : ReadNullableLong(runtimeState[key]);
            if (!value.HasValue)
            {
                return false;
            }

            int next = value.Value > int.MaxValue
                ? int.MaxValue
                : value.Value < int.MinValue
                    ? int.MinValue
                    : (int)value.Value;
            if (target == next)
            {
                return false;
            }

            target = next;
            return true;
        }

        private static bool ReplaceRuntimeIntMap(JToken token, Dictionary<string, int> target)
        {
            if (token == null || target == null)
            {
                return false;
            }

            Dictionary<string, int> next = ReadRuntimeIntMap(token);
            if (DictionaryEquals(target, next))
            {
                return false;
            }

            target.Clear();
            foreach (KeyValuePair<string, int> pair in next)
            {
                target[pair.Key] = pair.Value;
            }

            return true;
        }

        private static Dictionary<string, int> ReadRuntimeIntMap(JToken token)
        {
            Dictionary<string, int> values = new Dictionary<string, int>(StringComparer.Ordinal);
            if (token is JObject obj)
            {
                foreach (JProperty property in obj.Properties())
                {
                    long? value = ReadNullableLong(property.Value);
                    if (string.IsNullOrWhiteSpace(property.Name) || !value.HasValue || value.Value <= 0)
                    {
                        continue;
                    }

                    values[property.Name] = value.Value > int.MaxValue ? int.MaxValue : (int)value.Value;
                }
            }
            else if (token is JArray array)
            {
                for (int i = 0; i < array.Count; i++)
                {
                    if (!(array[i] is JObject entry))
                    {
                        continue;
                    }

                    string itemId = ReadString(entry["itemId"]);
                    long? value = ReadNullableLong(entry["acquiredCount"]);
                    if (string.IsNullOrWhiteSpace(itemId) || !value.HasValue || value.Value <= 0)
                    {
                        continue;
                    }

                    values[itemId] = value.Value > int.MaxValue ? int.MaxValue : (int)value.Value;
                }
            }

            return values;
        }

        private static bool ReplaceRuntimeStringSet(JToken token, HashSet<string> target)
        {
            if (token == null || target == null)
            {
                return false;
            }

            HashSet<string> next = ReadRuntimeStringSet(token);
            if (target.SetEquals(next))
            {
                return false;
            }

            target.Clear();
            foreach (string value in next)
            {
                target.Add(value);
            }

            return true;
        }

        private static HashSet<string> ReadRuntimeStringSet(JToken token)
        {
            HashSet<string> values = new HashSet<string>(StringComparer.Ordinal);
            if (token is JObject obj)
            {
                foreach (JProperty property in obj.Properties())
                {
                    if (!string.IsNullOrWhiteSpace(property.Name) && IsRuntimeSetValueEnabled(property.Value))
                    {
                        values.Add(property.Name);
                    }
                }
            }
            else if (token is JArray array)
            {
                for (int i = 0; i < array.Count; i++)
                {
                    string value = string.Empty;
                    if (array[i] is JObject entry)
                    {
                        value = ReadString(entry["itemId"]);
                        if (string.IsNullOrWhiteSpace(value))
                        {
                            value = ReadString(entry["claimId"]);
                        }

                        if (string.IsNullOrWhiteSpace(value))
                        {
                            value = ReadString(entry["rewardId"]);
                        }
                    }
                    else
                    {
                        value = ReadString(array[i]);
                    }

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        values.Add(value);
                    }
                }
            }

            return values;
        }

        private static bool IsRuntimeSetValueEnabled(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return false;
            }

            if (token.Type == JTokenType.Boolean)
            {
                return token.Value<bool>();
            }

            long? numeric = ReadNullableLong(token);
            if (numeric.HasValue)
            {
                return numeric.Value > 0;
            }

            string value = token.ToString();
            return value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("TRUE", StringComparison.Ordinal) ||
                   value.Equals("1", StringComparison.Ordinal);
        }

        private static bool DictionaryEquals(Dictionary<string, int> left, Dictionary<string, int> right)
        {
            if (left == null || right == null || left.Count != right.Count)
            {
                return false;
            }

            foreach (KeyValuePair<string, int> pair in left)
            {
                if (!right.TryGetValue(pair.Key, out int value) || value != pair.Value)
                {
                    return false;
                }
            }

            return true;
        }

        private bool ApplyCloudScriptHiddenCashResponse(JObject root)
        {
            if (_context == null || _context.State == null || root == null)
            {
                return false;
            }

            JObject dataObject = ReadMutationDataObject(root);
            long? cashAfter = ReadMutationLong(root, dataObject, "cashAfter");
            if (!cashAfter.HasValue &&
                ReadMutationToken(root, dataObject, "cshRuntimeState") is JObject runtimeState)
            {
                cashAfter = ReadNullableLong(runtimeState["cash"]);
            }

            if (!cashAfter.HasValue || _context.State.cash == cashAfter.Value)
            {
                return false;
            }

            _context.State.cash = cashAfter.Value;
            return true;
        }

        private bool ApplyCurrencyValue(string currencyCode, long? amount)
        {
            if (!amount.HasValue || _context == null || _context.State == null)
            {
                return false;
            }

            string normalized = string.IsNullOrWhiteSpace(currencyCode) ? "GD" : currencyCode;
            if (string.Equals(normalized, "GD", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "gold", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "softCurrency", StringComparison.OrdinalIgnoreCase))
            {
                if (_context.State.softCurrency == amount.Value)
                {
                    return false;
                }

                _context.State.softCurrency = amount.Value;
                return true;
            }

            if (string.Equals(normalized, "PP", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "prismPearl", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "pearl", StringComparison.OrdinalIgnoreCase))
            {
                if (_context.State.prismPearl == amount.Value)
                {
                    return false;
                }

                _context.State.prismPearl = amount.Value;
                return true;
            }

            if (string.Equals(normalized, "PC", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "pirateCoin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "coin", StringComparison.OrdinalIgnoreCase))
            {
                if (_context.State.pirateCoin == amount.Value)
                {
                    return false;
                }

                _context.State.pirateCoin = amount.Value;
                return true;
            }

            return false;
        }

        private static void LogCookingMutationDiagnostic(string operation, string message)
        {
            if (!CookingMutationDiagnosticsEnabled || !IsCookingMutationOperation(operation))
            {
                return;
            }

            Debug.Log("[FISHER_COOK_DIAG][Bridge][" + operation + "] " + message);
        }

        private string ActiveCookingStateForDiagnostics()
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

        private static string JsonForDiagnostics(object value)
        {
            if (value == null)
            {
                return "null";
            }

            try
            {
                JObject obj = JObject.FromObject(value);
                obj["clientBuildId"] = CookingMutationClientBuildId;
                return obj.ToString(Newtonsoft.Json.Formatting.None);
            }
            catch (Exception)
            {
                return value.ToString();
            }
        }

        private static string TruncateForDiagnostics(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= CookingMutationRawJsonLogLimit)
            {
                return value ?? string.Empty;
            }

            return value.Substring(0, CookingMutationRawJsonLogLimit) +
                   "...(truncated " + value.Length + " chars)";
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

        private static string ReadString(JToken token)
        {
            return token == null || token.Type == JTokenType.Null ? string.Empty : token.ToString();
        }

        private static JObject ReadMutationDataObject(JObject root)
        {
            return root == null ? null : root["data"] as JObject;
        }

        private static JToken ReadMutationToken(JObject root, JObject dataObject, string key, string alternateKey = null)
        {
            if (root == null || string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            JToken token = root[key];
            if (token == null && !string.IsNullOrWhiteSpace(alternateKey))
            {
                token = root[alternateKey];
            }

            if (token == null && dataObject != null)
            {
                token = dataObject[key];
                if (token == null && !string.IsNullOrWhiteSpace(alternateKey))
                {
                    token = dataObject[alternateKey];
                }
            }

            return token;
        }

        private static string ReadMutationString(JObject root, JObject dataObject, string key)
        {
            return ReadString(ReadMutationToken(root, dataObject, key));
        }

        private static int ReadMutationInt(JObject root, JObject dataObject, string key, int fallback)
        {
            return ReadInt(ReadMutationToken(root, dataObject, key), fallback);
        }

        private static long? ReadMutationLong(JObject root, JObject dataObject, string key)
        {
            return ReadNullableLong(ReadMutationToken(root, dataObject, key));
        }

        private static int ReadInt(JToken token, int fallback)
        {
            long? value = ReadNullableLong(token);
            if (!value.HasValue)
            {
                return fallback;
            }

            if (value.Value > int.MaxValue)
            {
                return int.MaxValue;
            }

            if (value.Value < int.MinValue)
            {
                return int.MinValue;
            }

            return (int)value.Value;
        }

        private static bool ReadBool(JToken token, bool fallback)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return fallback;
            }

            if (token.Type == JTokenType.Boolean)
            {
                return token.Value<bool>();
            }

            long? numeric = ReadNullableLong(token);
            if (numeric.HasValue)
            {
                return numeric.Value != 0L;
            }

            string value = token.ToString();
            if (bool.TryParse(value, out bool parsed))
            {
                return parsed;
            }

            return fallback;
        }

        private static long? ReadNullableLong(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return null;
            }

            if (token.Type == JTokenType.Integer)
            {
                return token.Value<long>();
            }

            if (long.TryParse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed))
            {
                return parsed;
            }

            return null;
        }

        [Serializable]
        private sealed class CloudScriptMutationResponse
        {
            public bool success = false;
            public string error = string.Empty;
            public string errorCode = string.Empty;
            public string message = string.Empty;
            public string requestId = string.Empty;
            public bool duplicate = false;
            public string actionType = string.Empty;
            public string serverNowUtc = string.Empty;
            public string mutationAppliedAtUtc = string.Empty;
            public string cloudScriptBuildId = string.Empty;
            public string cshBuildId = string.Empty;
            public string rewardId = string.Empty;
            public string claimId = string.Empty;
            public int claimedCookCount = 0;
            public int addedItemCount = 0;
            public string outputItemId = string.Empty;
            public int capacityAfter = 0;
            public int bagCapacityLevel = -1;
            [NonSerialized] public string rawJson = string.Empty;
        }

        private struct CookingRequestIdentity
        {
            public int slotIndex;
            public string recipeId;
            public string startedAtUtc;
            public long startedUtcTicks;
        }

        private static bool TryGetInventoryKeyByItemId(string itemId, out string inventoryKey)
        {
            inventoryKey = string.Empty;
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return false;
            }

            if (itemId.StartsWith("fish_", StringComparison.Ordinal))
            {
                inventoryKey = FishInventoryKey;
                return true;
            }

            if (itemId.StartsWith("food_", StringComparison.Ordinal))
            {
                inventoryKey = FoodInventoryKey;
                return true;
            }

            if (itemId.StartsWith("mat_", StringComparison.Ordinal))
            {
                inventoryKey = IngredientInventoryKey;
                return true;
            }

            if (itemId.StartsWith("ticket_", StringComparison.Ordinal) ||
                itemId.StartsWith("box_", StringComparison.Ordinal) ||
                itemId.StartsWith("fragment_", StringComparison.Ordinal))
            {
                inventoryKey = OddmentInventoryKey;
                return true;
            }

            return false;
        }

        private void LogPlayFabInventoryQueueBridge(string action, string itemId, int amount)
        {
            if (!_logPlayFabInventoryQueueBridge || _playFabInventoryQueueBridgeLogged)
            {
                return;
            }

            _playFabInventoryQueueBridgeLogged = true;
            Debug.Log("[FisherPlayerDataBridge] PlayFab Inventory 큐 연결 활성화: CSH itemId 변화량을 PlayFabGateway.Inventory." +
                      action + "(string,int)에 전달합니다. itemId=" + itemId +
                      ", amount=" + amount + ". CSH UI는 로컬 판매 확정/골드 지급을 하지 않고 " +
                      "RefreshAllPlayerData 이후 표시 snapshot만 갱신합니다. 반복 큐 병합/Flush 제한은 YWJ InventoryGateway 정책을 확인하세요.");
        }

        private void LogPlayerDataWarning(System.Exception exception)
        {
            if (_playerDataWarningLogged)
            {
                return;
            }

            _playerDataWarningLogged = true;
            Debug.LogWarning("[FisherPlayerDataBridge] PlayerData 동기화를 건너뜁니다. DataCenter 구성을 확인하세요: " +
                             exception.Message);
        }

        private void LogPlayFabDataStoreWarning(string message)
        {
            if (_playFabDataStoreWarningLogged)
            {
                return;
            }

            _playFabDataStoreWarningLogged = true;
            Debug.LogWarning("[FisherPlayerDataBridge] " + message);
        }

        private void TryInjectEquipmentWallet()
        {
            EquipmentManager equipmentManager = FindFirstObjectByType<EquipmentManager>();
            if (equipmentManager == null)
            {
                return;
            }

            equipmentManager.SetGoldWallet(this);
        }

        private void TryInjectEquipmentMaterialInventory()
        {
            EquipmentManager equipmentManager = FindFirstObjectByType<EquipmentManager>();
            if (equipmentManager == null)
            {
                return;
            }

            _materialInventoryAdapter ??= new EquipmentMaterialInventoryAdapter(_context, this);
            equipmentManager.SetMaterialInventory(_materialInventoryAdapter);
        }

        private bool ShouldPreserveFixtureGold()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            return activeScene.IsValid() && activeScene.name == WorkSceneName;
        }

        #endregion

        private struct PlayerDataSnapshot
        {
            public long Gold;
            public long PrismPearl;
            public long PirateCoin;
            public int CurrentStage;
            public int FarthestStage;
        }

        private sealed class EquipmentMaterialInventoryAdapter : IMaterialInventory
        {
            private readonly FisherRuntimeContext context;
            private readonly FisherPlayerDataBridge bridge;
            private readonly IMaterialInventory fallback = new DummyMaterialInventory();

            public EquipmentMaterialInventoryAdapter(FisherRuntimeContext context, FisherPlayerDataBridge bridge)
            {
                this.context = context;
                this.bridge = bridge;
            }

            private InventoryService Service
                => context != null && context.IsReady ? context.InventoryService : null;

            public int GetCount(string materialId)
            {
                InventoryService service = Service;
                return service == null ? fallback.GetCount(materialId) : service.CountItem(materialId);
            }

            public bool Has(IReadOnlyList<MaterialCost> costs)
            {
                InventoryService service = Service;
                if (service == null)
                {
                    return fallback.Has(costs);
                }

                List<MaterialCost> needs = Aggregate(costs);
                for (int i = 0; i < needs.Count; i++)
                {
                    MaterialCost need = needs[i];
                    if (service.CountItem(need.materialId) < need.count)
                    {
                        return false;
                    }
                }

                return true;
            }

            public bool TryConsume(IReadOnlyList<MaterialCost> costs)
            {
                InventoryService service = Service;
                if (service == null)
                {
                    return fallback.TryConsume(costs);
                }

                List<MaterialCost> needs = Aggregate(costs);
                for (int i = 0; i < needs.Count; i++)
                {
                    MaterialCost need = needs[i];
                    if (service.CountItem(need.materialId) < need.count)
                    {
                        return false;
                    }
                }

                List<MaterialCost> consumed = new List<MaterialCost>();
                for (int i = 0; i < needs.Count; i++)
                {
                    MaterialCost need = needs[i];
                    ServiceResult result = service.TryConsumeItem(need.materialId, need.count);
                    if (result == null || !result.Success)
                    {
                        Rollback(service, consumed);
                        return false;
                    }

                    consumed.Add(need);
                }

                SyncConsumedMaterials(consumed);
                return true;
            }

            private void SyncConsumedMaterials(List<MaterialCost> consumed)
            {
                if (bridge == null || consumed == null)
                {
                    return;
                }

                for (int i = 0; i < consumed.Count; i++)
                {
                    MaterialCost material = consumed[i];
                    if (!bridge.TrySyncItemDeltaToPlayerData(material.materialId, -material.count))
                    {
                        Debug.LogWarning("[FisherPlayerDataBridge] Material sync skipped: " + material.materialId);
                    }
                }
            }

            private static void Rollback(InventoryService service, List<MaterialCost> consumed)
            {
                if (service == null || consumed == null)
                {
                    return;
                }

                for (int i = 0; i < consumed.Count; i++)
                {
                    MaterialCost material = consumed[i];
                    service.TryAddItem(material.materialId, material.count);
                }
            }

            private static List<MaterialCost> Aggregate(IReadOnlyList<MaterialCost> costs)
            {
                Dictionary<string, int> sums = new Dictionary<string, int>();
                if (costs != null)
                {
                    for (int i = 0; i < costs.Count; i++)
                    {
                        MaterialCost cost = costs[i];
                        if (string.IsNullOrWhiteSpace(cost.materialId) || cost.count <= 0)
                        {
                            continue;
                        }

                        sums[cost.materialId] = sums.TryGetValue(cost.materialId, out int count)
                            ? count + cost.count
                            : cost.count;
                    }
                }

                List<MaterialCost> result = new List<MaterialCost>(sums.Count);
                foreach (KeyValuePair<string, int> pair in sums)
                {
                    result.Add(new MaterialCost
                    {
                        materialId = pair.Key,
                        count = pair.Value
                    });
                }

                return result;
            }
        }
    }
}
