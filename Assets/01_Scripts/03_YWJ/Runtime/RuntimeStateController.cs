using Runtime.RSM;
using System;
using UI;
using UnityEngine;
using System.Threading.Tasks;
using OfflineReward;
using RMS.UI;
using RMS.Fishing;


namespace Runtime
{
    public static class HasAlreadyLoginCache
    {
        public static bool HasAlreadyLogin = false;
    }


    public class RuntimeStateController : MonoBehaviour
    {
        [Header("Current State")]
        [SerializeField] private RuntimeState _currentState = RuntimeState.GAMESTART;

        [Header("UI Controller")]
        [SerializeField] private RuntimeUIController _runtimeUIController;

        [Header("Loading UI Panel")]
        [SerializeField] private GameObject _loadingUIPanel;

        // 0702 RMS 추가
        [Header("보스 스테이지 판단 (MAINSTAGE 요청 시 보스전 진행 중이면 BOSSSTAGE로 보정)")]
        [SerializeField] private BossStageActivator _bossStageActivator;
        [SerializeField] private FishSpawnManager _fishSpawnManager;

        private OfflineRewardCaculator _offlineRewardCaculator;

        public RuntimeState CurrentState
        {
            get { return _currentState; }
            set
            {
                // 0702 RMS 수정
                RuntimeState resolved = ResolveBossStageOverride(value);
                if (_currentState != resolved)
                {
                    _currentState = resolved;
                    OnChangeRuntimeStateAsync(_currentState);
                }
            }
        }

        // MAINSTAGE 전환 요청이 들어와도 보스 스테이지가 아직 진행 중이면 BOSSSTAGE로 보정한다.
        // (기존엔 RuntimeUIController가 화면만 보스 패널로 덮어씌우고 _currentState는 MAINSTAGE로 남아,
        //  탭 이동 후 보스 클리어 시 CurrentState=MAINSTAGE 재대입이 "값 동일"로 무시되는 버그가 있었음)
        private RuntimeState ResolveBossStageOverride(RuntimeState requested)
        {
            if (requested != RuntimeState.MAINSTAGE) return requested;
            if (_bossStageActivator == null || !_bossStageActivator.IsBossStageActive) return requested;
            if (_fishSpawnManager == null || _fishSpawnManager.CurrentStage == null || !_fishSpawnManager.CurrentStage.IsBossStage)
                return requested;

            return RuntimeState.BOSSSTAGE;
        }

        private bool isChangingState;

        private Root_RSM _currentRSM;

        private GameStart_RSM _gameStart_RSM;
        private Login_RSM _login_RSM;
        private OfflineReward_RSM _offlineReward_RSM;
        private EquipmentUpgrade_RSM _equipmentUpgrade_RSM;
        private ShipUpgrade_RSM _shipUpgrade_RSM;
        private SailorUpgrade_RSM _sailorUpgrade_RSM;
        private Cooking_RSM _cooking_RSM;
        private Inventory_RSM _inventory_RSM;
        private Shop_RSM _shop_RSM;
        private Recruit_RSM _recruit_RSM;
        private Collection_RSM _collection_RSM;
        private MainStage_RSM _mainStage_RSM;
        private StageChange_RSM _stageChange_RSM;
        private BossStage_RSM bossStage_RSM;
        private MultiMatching_RSM _multiMatching_RSM;
        private MultiStage_RSM _multiStage_RSM;


        private void Awake()
        {
            Init_RuntimeStates();

            _offlineRewardCaculator = FindFirstObjectByType<OfflineRewardCaculator>();

            // 0702 RMS 추가
            if (_bossStageActivator == null)
                _bossStageActivator = FindFirstObjectByType<BossStageActivator>();
            if (_fishSpawnManager == null)
                _fishSpawnManager = FindFirstObjectByType<FishSpawnManager>();
        }

        private void Start()
        {
            // 0623 수정, 로그인 여부에 따라 초기 상태를 결정하도록 변경
            if (HasAlreadyLoginCache.HasAlreadyLogin)
            {
                Debug.Log($"이미 로그인된 상태입니다. MAINSTAGE로 이동합니다. {HasAlreadyLoginCache.HasAlreadyLogin}");
                CurrentState = RuntimeState.MAINSTAGE;
            }
            else
            {
                Debug.Log($"로그인되지 않은 상태입니다. {_currentState}로 이동합니다. {HasAlreadyLoginCache.HasAlreadyLogin}");
                OnChangeRuntimeStateAsync(_currentState);
            }
        }

        // Update is called once per frame
        private void Update()
        {
            if (_currentRSM != null)
            {
                _currentRSM.UpdateState();
            }
        }

        // 0622 추가, 상태 전환 시 필요한 데이터 로딩이 비동기로 처리되어야 하는 경우를 대비하여, 상태 전환 메서드를 비동기로 변경한다.
        private void OnChangeRuntimeStateAsync(RuntimeState nextState)
        {
            _ = ChangeRuntimeStateAsync(nextState);
        }



        private async Task ChangeRuntimeStateAsync(RuntimeState nextState)
        {
            if (isChangingState)
            {
                Debug.LogWarning("상태 전환 중입니다. 잠시 기다려주세요.");
                return;
            }

            isChangingState = true;
            _loadingUIPanel.SetActive(true); // 상태 전환 시작 시 로딩 UI 활성화

            try
            {
                _currentRSM?.ExitState();

                _currentState = nextState;

                await LoadDataForStateAsync(_currentState);

                _currentRSM = _currentState switch
                {
                    RuntimeState.GAMESTART => _gameStart_RSM,
                    RuntimeState.LOGIN => _login_RSM,
                    RuntimeState.OFFLINEREWARD => _offlineReward_RSM,
                    RuntimeState.EQUIPMENTUPGRADE => _equipmentUpgrade_RSM,
                    RuntimeState.SHIPUPGRADE => _shipUpgrade_RSM,
                    RuntimeState.SAILORUPGRADE => _sailorUpgrade_RSM,
                    RuntimeState.COOKING => _cooking_RSM,
                    RuntimeState.INVENTORY => _inventory_RSM,
                    RuntimeState.SHOP => _shop_RSM,
                    RuntimeState.RECRUIT => _recruit_RSM,
                    RuntimeState.COLLECTION => _collection_RSM,
                    RuntimeState.MAINSTAGE => _mainStage_RSM,
                    RuntimeState.STAGECHANGE => _stageChange_RSM,
                    RuntimeState.BOSSSTAGE => bossStage_RSM,
                    RuntimeState.MULTIMATCHING => _multiMatching_RSM,
                    RuntimeState.MULTISTAGE => _multiStage_RSM,
                    _ => null
                };

                _currentRSM.EnterState();

                if (_runtimeUIController != null)
                {
                    _runtimeUIController.SetActiveRuntimeUI(_currentState);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"상태 전환 실패: {e}");
            }
            finally
            {
                isChangingState = false;
                _loadingUIPanel.SetActive(false); // 상태 전환 완료 시 로딩 UI 비활성화
            }
        }


        private async Task LoadDataForStateAsync(RuntimeState nextState)
        {
            var gateway = PlayFabGateway.Instance;

            if (gateway == null)
            {
                Debug.LogWarning("PlayFabGateway.Instance가 null입니다.");
                return;
            }

            switch (nextState)
            {
                case RuntimeState.GAMESTART:
                    break;

                case RuntimeState.LOGIN:
                    break;

                case RuntimeState.OFFLINEREWARD:
                    await gateway.Stage.RefreshStageDataAsync();
                    await _offlineRewardCaculator.ProcessOfflineReward();
                    break;

                case RuntimeState.EQUIPMENTUPGRADE:
                    // 장비 업그레이드 화면 진입 시 필요한 데이터
                    await gateway.Equipment.RefreshEquipmentDataAsync();
                    await gateway.Money.RefreshMoneyDataAsync();
                    Debug.Log("EQUIPMENTUPGRADE: 장비 및 화폐 데이터 로드 완료");
                    break;

                case RuntimeState.SHIPUPGRADE:
                    // 선박 업그레이드 화면 진입 시 필요한 데이터
                    await gateway.Ship.RefreshShipDataAsync();
                    await gateway.Money.RefreshMoneyDataAsync();
                    Debug.Log("SHIPUPGRADE: 선박 및 화폐 데이터 로드 완료");
                    break;

                case RuntimeState.SAILORUPGRADE:
                    // 선원(크루) 업그레이드 화면 진입 시 필요한 데이터
                    await gateway.Crew.RefreshCrewDataAsync();
                    await gateway.Money.RefreshMoneyDataAsync();
                    Debug.Log("SAILORUPGRADE: 크루 및 화폐 데이터 로드 완료");
                    break;

                case RuntimeState.COOKING:
                    // 요리 화면 진입 시 필요한 데이터
                    await gateway.Cooking.RefreshCookingDataAsync();
                    await gateway.Inventory.RefreshInventoryDataAsync();
                    Debug.Log("COOKING: 요리 및 인벤토리 데이터 로드 완료");
                    break;

                case RuntimeState.INVENTORY:
                    // 인벤토리 화면 진입 시 필요한 데이터
                    await gateway.Inventory.RefreshInventoryDataAsync();
                    await gateway.Money.RefreshMoneyDataAsync();
                    Debug.Log("INVENTORY: 인벤토리 및 화폐 데이터 로드 완료");
                    break;

                case RuntimeState.SHOP:
                    // 상점 화면 진입 시 필요한 데이터
                    await gateway.Money.RefreshMoneyDataAsync();
                    await gateway.Inventory.RefreshInventoryDataAsync();
                    Debug.Log("SHOP: 화폐 및 인벤토리 데이터 로드 완료");
                    break;

                case RuntimeState.RECRUIT:
                    // 모집 화면 진입 시 필요한 데이터
                    await gateway.Crew.RefreshCrewDataAsync();
                    await gateway.Money.RefreshMoneyDataAsync();
                    Debug.Log("RECRUIT: 크루 및 화폐 데이터 로드 완료");
                    break;

                case RuntimeState.COLLECTION:
                    // 도감 화면 진입 시 필요한 데이터
                    await gateway.Inventory.RefreshInventoryDataAsync();
                    Debug.Log("COLLECTION: 모든 컬렉션 데이터 로드 완료");
                    break;

                case RuntimeState.MAINSTAGE:
                    // 메인 스테이지 진입 시 필요한 데이터
                    await gateway.Ship.RefreshShipDataAsync();
                    await gateway.Crew.RefreshCrewDataAsync();
                    await gateway.Equipment.RefreshEquipmentDataAsync();
                    await gateway.Money.RefreshMoneyDataAsync();
                    await gateway.Stage.RefreshStageDataAsync();
                    Debug.Log("MAINSTAGE: 스테이지 관련 데이터 로드 완료");
                    break;

                case RuntimeState.STAGECHANGE:
                    // 스테이지 변경 시 필요한 데이터
                    await gateway.Stage.RefreshStageDataAsync();
                    Debug.Log("STAGECHANGE: 스테이지 데이터 로드 완료");
                    break;

                case RuntimeState.BOSSSTAGE:
                    // 보스 스테이지 진입 시 필요한 데이터
                    await gateway.Ship.RefreshShipDataAsync();
                    await gateway.Crew.RefreshCrewDataAsync();
                    await gateway.Equipment.RefreshEquipmentDataAsync();
                    await gateway.Stage.RefreshStageDataAsync();
                    Debug.Log("BOSSSTAGE: 보스 스테이지 데이터 로드 완료");
                    break;

                case RuntimeState.MULTIMATCHING:
                    // 멀티 매칭 화면에서는 데이터 로드 불필요
                    Debug.Log("MULTIMATCHING: 데이터 로드 없음");
                    break;

                case RuntimeState.MULTISTAGE:
                    // 멀티 스테이지 진입 시 필요한 데이터
                    await gateway.Ship.RefreshShipDataAsync();
                    await gateway.Crew.RefreshCrewDataAsync();
                    await gateway.Equipment.RefreshEquipmentDataAsync();
                    Debug.Log("MULTISTAGE: 멀티 스테이지 데이터 로드 완료");
                    break;

                default:
                    Debug.LogWarning($"LoadDataForStateAsync: 처리되지 않은 상태 - {nextState}");
                    break;
            }
        }


        #region RuntimeState 초기화

        private void Init_RuntimeStates()
        {
            _gameStart_RSM = new GameStart_RSM();

            _login_RSM = new Login_RSM();

            _offlineReward_RSM = new OfflineReward_RSM();

            _equipmentUpgrade_RSM = new EquipmentUpgrade_RSM();

            _shipUpgrade_RSM = new ShipUpgrade_RSM();

            _sailorUpgrade_RSM = new SailorUpgrade_RSM();

            _cooking_RSM = new Cooking_RSM();

            _inventory_RSM = new Inventory_RSM();

            _shop_RSM = new Shop_RSM();

            _recruit_RSM = new Recruit_RSM();

            _collection_RSM = new Collection_RSM();

            _mainStage_RSM = new MainStage_RSM();

            _stageChange_RSM = new StageChange_RSM();

            bossStage_RSM = new BossStage_RSM();

            _multiMatching_RSM = new MultiMatching_RSM();

            _multiStage_RSM = new MultiStage_RSM();
        }

        #endregion
    }
}
