using UnityEngine;

namespace Fisher.PlayerSystems
{
    /// <summary>
    /// 00_MainScene의 CSH 루트에 미리 배치한 CSH 런타임/패널 스크립트를 연결합니다.
    /// 런타임에 GameObject나 컴포넌트를 생성하지 않습니다.
    /// </summary>
    public sealed class FisherRuntimeControlTower : MonoBehaviour
    {
        #region Inspector References

        [Header("Core")]
        [SerializeField] private FisherRuntimeContext _context;
        [SerializeField] private FisherPlayerDataBridge _playerDataBridge;

        [Header("Panel Adapters")]
        [SerializeField] private BagPanelAdapter _bagAdapter;
        [SerializeField] private CookingPanelAdapter _cookingAdapter;
        [SerializeField] private ShopPanelAdapter _shopAdapter;
        [SerializeField] private CollectionPanelAdapter _collectionAdapter;
        [SerializeField] private FisherFishingCatchAdapter _fishingCatchAdapter;

        [Header("Canvas View Roots")]
        [SerializeField] private GameObject _inventoryPanel;
        [SerializeField] private GameObject _cookingPanel;
        [SerializeField] private GameObject _shopPanel;
        [SerializeField] private GameObject _collectionPanel;

        private bool _bootstrapped;
        #endregion

        #region Unity Lifecycle

        private void Reset()
        {
            ResolveChildReferences();
        }

        #endregion

        #region Bootstrap

        [ContextMenu("Bootstrap CSH Runtime")]
        public void Bootstrap()
        {
            ResolveChildReferences();
            if (_bootstrapped)
            {
                return;
            }

            if (_context == null)
            {
                Debug.LogWarning("[FisherRuntimeControlTower] FisherRuntimeContext reference is missing.");
                return;
            }

            if (!_context.IsReady)
            {
                _context.Initialize();
            }

            FisherRuntimeUi.SetActiveProfile(_context.UiArtProfile);
            ConfigurePlayerDataBridge();
            ConfigurePanels();
            ConfigureFishingCatchAdapter();

            _bootstrapped = true;
        }

        private void ConfigurePlayerDataBridge()
        {
            if (_playerDataBridge == null)
            {
                Debug.LogWarning("[FisherRuntimeControlTower] FisherPlayerDataBridge reference is missing.");
                return;
            }

            _playerDataBridge.Configure(_context);
            _playerDataBridge.TryHydrateBagSnapshotsFromPlayFabDataStore(
                forceInventory: true,
                notify: false,
                out _,
                out _,
                out _);
        }

        private void ConfigurePanels()
        {
            ConfigureBag();
            ConfigureCooking();
            ConfigureShop();
            ConfigureCollection();
        }

        private void ConfigureBag()
        {
            if (_bagAdapter == null || _inventoryPanel == null)
            {
                LogMissingPanel("Bag", _bagAdapter, _inventoryPanel);
                return;
            }

            _bagAdapter.Configure(_context, _inventoryPanel);
            _bagAdapter.Refresh();
        }

        private void ConfigureCooking()
        {
            if (_cookingAdapter == null || _cookingPanel == null)
            {
                LogMissingPanel("Cooking", _cookingAdapter, _cookingPanel);
                return;
            }

            _cookingAdapter.Configure(_context, _cookingPanel);
            _cookingAdapter.Refresh();
        }

        private void ConfigureShop()
        {
            if (_shopAdapter == null || _shopPanel == null)
            {
                LogMissingPanel("Shop", _shopAdapter, _shopPanel);
                return;
            }

            _shopAdapter.Configure(_context, _shopPanel);
            _shopAdapter.Refresh();
        }

        private void ConfigureCollection()
        {
            if (_collectionAdapter == null || _collectionPanel == null)
            {
                LogMissingPanel("Collection", _collectionAdapter, _collectionPanel);
                return;
            }

            _collectionAdapter.Configure(_context, _collectionPanel);
            _collectionAdapter.Refresh();
        }

        private void ConfigureFishingCatchAdapter()
        {
            if (_fishingCatchAdapter != null)
            {
                _fishingCatchAdapter.Configure(_context);
            }
        }

        #endregion

        #region Reference Resolution

        private void ResolveChildReferences()
        {
            _context ??= GetComponentInChildren<FisherRuntimeContext>(true);
            _playerDataBridge ??= GetComponentInChildren<FisherPlayerDataBridge>(true);
            _bagAdapter ??= GetComponentInChildren<BagPanelAdapter>(true);
            _cookingAdapter ??= GetComponentInChildren<CookingPanelAdapter>(true);
            _shopAdapter ??= GetComponentInChildren<ShopPanelAdapter>(true);
            _collectionAdapter ??= GetComponentInChildren<CollectionPanelAdapter>(true);
            _fishingCatchAdapter ??= GetComponentInChildren<FisherFishingCatchAdapter>(true);
        }

        private static void LogMissingPanel(string panelName, Component adapter, GameObject viewRoot)
        {
            if (adapter == null && viewRoot == null)
            {
                Debug.LogWarning("[FisherRuntimeControlTower] " + panelName + " adapter and view root references are missing.");
                return;
            }

            if (adapter == null)
            {
                Debug.LogWarning("[FisherRuntimeControlTower] " + panelName + " adapter reference is missing.");
                return;
            }

            if (viewRoot == null)
            {
                Debug.LogWarning("[FisherRuntimeControlTower] " + panelName + " view root reference is missing.");
            }
        }

        #endregion
    }
}
