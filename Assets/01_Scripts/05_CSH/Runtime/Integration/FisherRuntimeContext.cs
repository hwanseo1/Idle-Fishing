using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fisher.PlayerSystems
{
    /// <summary>
    /// Fisher SO/JSON 카탈로그, 런타임 상태, 서비스 객체를 생성하는 로컬 composition root입니다.
    /// 외부 팀 연결은 상태 직접 변경보다 이 컨텍스트의 서비스나 전용 어댑터를 통해 들어와야 합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FisherRuntimeContext : MonoBehaviour
    {
        #region Inspector Preview Seed

        private const string RuntimeCatalogResourcePath = "05_CSH/RuntimeCatalog/FisherRuntimeCatalogSource";

        [Header("Preview Seed")]
        [SerializeField] private FisherRuntimeCatalogSource _catalogSource;
        [SerializeField] private long _initialSoftCurrency = 2000;
        [SerializeField] private long _initialPrismPearl = 0;
        [SerializeField] private long _initialPirateCoin = 0;
        [Tooltip("내부 결제 테스트용 hidden Cash입니다. 지갑 UI에는 표시하지 않고 Cash -> PP 프리미엄 상품 구매에만 사용합니다.")]
        [SerializeField] private long _initialHiddenCash = 100000;
        [SerializeField] private string[] _starterItemIds = { "ticket_speedup_10m" };
        [SerializeField] private int[] _starterItemCounts = { 1 };
        [SerializeField] private string _starterInstanceItemId = "";
        [SerializeField] private string _starterInstanceId = "";

        [Header("UI Skin")]
        [SerializeField] private FisherUiArtProfile _uiArtProfile;
        [SerializeField] private bool _autoLoadUiArtProfile = true;
        [SerializeField] private string _uiArtProfileResourcePath = FisherUiArtProfile.DefaultResourcePath;

        #endregion

        #region Runtime State

        private BalanceBuildResult _buildResult;
        private PlayerRuntimeState _state;
        private IClock _clock;
        private InventoryService _inventoryService;
        private RewardBundleService _rewardBundleService;
        private CookingService _cookingService;
        private ShopService _shopService;
        private CollectionService _collectionService;
        private BagQueryService _bagQueryService;
        private FisherUiArtProfile _resolvedUiArtProfile;
        private bool _uiArtProfileAssignedLogged;
        private bool _uiArtProfileLookupLogged;
        private bool _currencyHydratedFromPlayFabDataStore;
        private bool _inventoryHydratedFromPlayFabDataStore;
        private string _lastStatus = "초기화 전";

        /// <summary>
        /// 서비스 상태나 데이터가 바뀌어 패널을 다시 그려야 할 때 발생합니다.
        /// </summary>
        public event Action RuntimeChanged;

        /// <summary>
        /// 카탈로그와 주요 서비스가 모두 준비되었는지 확인합니다.
        /// </summary>
        public bool IsReady =>
            _buildResult != null &&
            _buildResult.Success &&
            _state != null &&
            _inventoryService != null &&
            _rewardBundleService != null &&
            _cookingService != null &&
            _shopService != null &&
            _collectionService != null &&
            _bagQueryService != null;

        /// <summary>
        /// UI에 표시할 수 있는 마지막 초기화 상태 메시지입니다.
        /// </summary>
        public string LastStatus => _lastStatus;

        /// <summary>
        /// 런타임 SO/JSON 빌드 결과와 검증 메시지를 포함한 카탈로그 생성 결과입니다.
        /// </summary>
        public BalanceBuildResult BuildResult => _buildResult;

        /// <summary>
        /// 현재 플레이어의 재화, 인벤토리, 도감, 요리 진행 상태입니다.
        /// </summary>
        public PlayerRuntimeState State => _state;

        /// <summary>
        /// 요리 완료 시간 검증에 사용하는 현재 시각 공급자입니다.
        /// </summary>
        public IClock Clock => _clock;

        /// <summary>
        /// 아이템 지급, 소비, 판매를 담당하는 서비스입니다.
        /// </summary>
        public InventoryService InventoryService => _inventoryService;

        /// <summary>
        /// 아이템/재화 보상 묶음을 원자적으로 적용하는 서비스입니다.
        /// </summary>
        public RewardBundleService RewardBundleService => _rewardBundleService;

        /// <summary>
        /// 레시피 시작과 완료 처리를 담당하는 서비스입니다.
        /// </summary>
        public CookingService CookingService => _cookingService;

        /// <summary>
        /// 상점 구매와 환불 보호를 담당하는 서비스입니다.
        /// </summary>
        public ShopService ShopService => _shopService;

        /// <summary>
        /// 획득 기반 도감 발견 상태와 보상 수령을 담당하는 서비스입니다.
        /// </summary>
        public CollectionService CollectionService => _collectionService;

        /// <summary>
        /// 가방 UI에 보여줄 정렬, 분류, 필터 snapshot을 생성하는 서비스입니다.
        /// </summary>
        public BagQueryService BagQueryService => _bagQueryService;

        public bool CurrencyHydratedFromPlayFabDataStore => _currencyHydratedFromPlayFabDataStore;

        public bool InventoryHydratedFromPlayFabDataStore => _inventoryHydratedFromPlayFabDataStore;

        public bool PlayerSnapshotHydrated =>
            _currencyHydratedFromPlayFabDataStore &&
            _inventoryHydratedFromPlayFabDataStore;

        /// <summary>
        /// 런타임 생성 UI에 적용할 Inspector/Resources 기반 공통 스킨입니다.
        /// </summary>
        public FisherUiArtProfile UiArtProfile => ResolveUiArtProfile();

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            Initialize();
        }

        private void OnValidate()
        {
            _initialHiddenCash = NonNegative(_initialHiddenCash);
            if (Application.isPlaying && _state != null)
            {
                ApplyInspectorHiddenCashToRuntime();
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// SO/JSON 런타임 카탈로그를 읽어 플레이어 런타임 상태를 새로 구성합니다.
        /// </summary>
        public void Initialize()
        {
            FisherRuntimeCatalogSource source = ResolveCatalogSource();
            _buildResult = FisherLocalCatalogBuilder.Build(source);
            ResetPlayFabSnapshotHydration();

            _state = new PlayerRuntimeState
            {
                softCurrency = _initialSoftCurrency,
                prismPearl = _initialPrismPearl,
                pirateCoin = _initialPirateCoin,
                cash = NonNegative(_initialHiddenCash)
            };
            _clock = new SystemClock();

            if (_buildResult == null || !_buildResult.Success)
            {
                _lastStatus = BuildFailureStatus(_buildResult);
                Debug.LogError("[FisherRuntimeContext] " + _lastStatus);
                RuntimeChanged?.Invoke();
                return;
            }

            ApplyInitialEconomyState();
            RebuildServices();
            SeedStarterItems();

            _lastStatus = "준비 완료";
            Debug.Log("[FisherRuntimeContext] Ready (SO+JSON catalog)");
            RuntimeChanged?.Invoke();
        }

        private FisherRuntimeCatalogSource ResolveCatalogSource()
        {
            if (_catalogSource != null)
            {
                return _catalogSource;
            }

            _catalogSource = Resources.Load<FisherRuntimeCatalogSource>(RuntimeCatalogResourcePath);
            if (_catalogSource == null)
            {
                Debug.LogError("[FisherRuntimeContext] Runtime catalog source is missing: Resources/" + RuntimeCatalogResourcePath);
            }

            return _catalogSource;
        }

        private FisherUiArtProfile ResolveUiArtProfile()
        {
            if (_uiArtProfile != null)
            {
                if (!_uiArtProfileAssignedLogged)
                {
                    _uiArtProfileAssignedLogged = true;
                    Debug.Log("[FisherRuntimeContext] UI art profile assigned in Inspector: " + _uiArtProfile.name);
                }

                return _uiArtProfile;
            }

            if (!_autoLoadUiArtProfile)
            {
                if (!_uiArtProfileLookupLogged)
                {
                    _uiArtProfileLookupLogged = true;
                    Debug.Log("[FisherRuntimeContext] UI art profile is not assigned and auto-load is disabled. CSH UI uses fallback skin.");
                }

                return null;
            }

            if (_resolvedUiArtProfile != null)
            {
                return _resolvedUiArtProfile;
            }

            _resolvedUiArtProfile = FisherUiArtProfile.LoadFromResources(_uiArtProfileResourcePath);
            if (_resolvedUiArtProfile != null)
            {
                Debug.Log("[FisherRuntimeContext] UI art profile loaded: " + _resolvedUiArtProfile.name);
                return _resolvedUiArtProfile;
            }

            if (!_uiArtProfileLookupLogged)
            {
                _uiArtProfileLookupLogged = true;
                Debug.Log("[FisherRuntimeContext] UI art profile not found. Create Resources/" + _uiArtProfileResourcePath + ".asset or assign Ui Art Profile in Inspector to skin generated CSH UI.");
            }

            return null;
        }

        /// <summary>
        /// 현재 카탈로그와 플레이어 상태를 기준으로 Fisher 서비스 객체를 다시 묶습니다.
        /// 상태 객체는 유지하고 서비스 참조만 재구성하는 경계입니다.
        /// </summary>
        public void RebuildServices()
        {
            BalanceCatalog catalog = _buildResult?.Catalog;
            _inventoryService = new InventoryService(catalog, _state);
            _rewardBundleService = new RewardBundleService(_state, _inventoryService);
            _cookingService = new CookingService(catalog, _state, _inventoryService, _clock);
            _shopService = new ShopService(catalog, _state, _inventoryService, _rewardBundleService);
            _collectionService = new CollectionService(catalog, _state, _inventoryService, _rewardBundleService);
            _bagQueryService = new BagQueryService(catalog, _state);
        }

        /// <summary>
        /// 서비스 작업 이후 패널이 같은 런타임 상태를 다시 읽도록 알립니다.
        /// </summary>
        public void NotifyRuntimeChanged()
        {
            RuntimeChanged?.Invoke();
        }

        public void MarkPlayFabSnapshotHydration(bool currencyHydrated, bool inventoryHydrated, string source)
        {
            if (currencyHydrated)
            {
                _currencyHydratedFromPlayFabDataStore = true;
            }

            if (inventoryHydrated)
            {
                _inventoryHydratedFromPlayFabDataStore = true;
            }
        }

        /// <summary>
        /// Play Mode 중 Inspector에서 hidden Cash를 바꿨을 때 현재 런타임 상태에 즉시 반영합니다.
        /// </summary>
        [ContextMenu("Apply Inspector Hidden Cash")]
        public void ApplyInspectorHiddenCashToRuntime()
        {
            if (_state == null)
            {
                return;
            }

            _state.cash = NonNegative(_initialHiddenCash);
            RuntimeChanged?.Invoke();
        }

        #endregion

        #region Seed Data

        private void ApplyInitialEconomyState()
        {
            if (_state == null)
            {
                return;
            }

            _state.bagCapacity = ReadEnabledEconomyInt("initial_bag_capacity", _state.bagCapacity);
            _state.softCurrency = ReadEnabledEconomyLong("starting_soft_currency", _state.softCurrency);
            if (_state.bagCapacity < 0)
            {
                _state.bagCapacity = 0;
            }

            if (_state.bagCapacityLevel < 0)
            {
                _state.bagCapacityLevel = 0;
            }

            _state.cookingSlotLimit = CookingService.FixedCookingSlotCount;
            _state.cookingSlotLevel = 0;
        }

        private void SeedStarterItems()
        {
            int count = Math.Min(_starterItemIds == null ? 0 : _starterItemIds.Length, _starterItemCounts == null ? 0 : _starterItemCounts.Length);
            List<string> failures = new List<string>();

            for (int i = 0; i < count; i++)
            {
                string itemId = _starterItemIds[i];
                int itemCount = _starterItemCounts[i];
                ServiceResult result = _inventoryService.TryAddItem(itemId, itemCount);
                if (result == null || !result.Success)
                {
                    failures.Add(itemId + ": " + (result == null ? "no result" : result.FailureReason));
                }
            }

            if (failures.Count > 0)
            {
                _lastStatus = "Ready with seed failures: " + string.Join(", ", failures);
                Debug.LogWarning("[FisherRuntimeContext] " + _lastStatus);
            }

            if (!string.IsNullOrWhiteSpace(_starterInstanceItemId) && !string.IsNullOrWhiteSpace(_starterInstanceId))
            {
                ServiceResult instanceResult = _inventoryService.TryAddItem(_starterInstanceItemId, 1, _starterInstanceId, 1);
                if (instanceResult == null || !instanceResult.Success)
                {
                    Debug.LogWarning("[FisherRuntimeContext] Instance seed failed: " + _starterInstanceItemId);
                }
            }

        }

        #endregion

        #region Hydration

        private void ResetPlayFabSnapshotHydration()
        {
            _currencyHydratedFromPlayFabDataStore = false;
            _inventoryHydratedFromPlayFabDataStore = false;
        }

        #endregion

        #region Catalog Helpers

        private int ReadEnabledEconomyInt(string key, int fallback)
        {
            long value = ReadEnabledEconomyLong(key, fallback);
            if (value <= 0)
            {
                return fallback;
            }

            return value >= int.MaxValue ? int.MaxValue : (int)value;
        }

        private long ReadEnabledEconomyLong(string key, long fallback)
        {
            long resolvedFallback = FisherEconomyDefaults.ResolveLong(key, fallback);
            BalanceCatalog catalog = _buildResult?.Catalog;
            if (catalog == null ||
                string.IsNullOrEmpty(key) ||
                !catalog.EconomyParamsByKey.TryGetValue(key, out EconomyParam param) ||
                param == null ||
                !param.IsEnabled ||
                !long.TryParse(param.Value, out long value))
            {
                return resolvedFallback;
            }

            return value;
        }

        private static long NonNegative(long value)
        {
            return value < 0 ? 0 : value;
        }

        private static string BuildFailureStatus(BalanceBuildResult result)
        {
            if (result == null)
            {
                return "카탈로그 생성 결과가 없습니다.";
            }

            if (result.Errors.Count == 0)
            {
                return "카탈로그 생성에 실패했습니다.";
            }

            return "카탈로그 생성 실패: " + string.Join(" / ", result.Errors);
        }

        #endregion
    }
}
