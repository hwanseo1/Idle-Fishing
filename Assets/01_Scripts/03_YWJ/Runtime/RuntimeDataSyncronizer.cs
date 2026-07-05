using RMS.Data;
using RMS.Fishing;
using UnityEngine;
using Fisher.PlayerSystems;
using System.Collections;
using Runtime;

public class RuntimeDataSyncronizer : MonoBehaviour
{
    [Header("Fish Spawn Manager")]
    [SerializeField] private FishSpawnManager _fishSpawnManager;

    [Header("Stage Dictionary Convertor")]
    [SerializeField] private StageDictionaryConvertor _stageDictionaryConvertor;

    [Header("FisherRuntimeContext")]
    [SerializeField] private FisherRuntimeContext _fisherRuntimeContext;

    [Header("Bottom UI Panel")]
    [SerializeField] private GameObject _bottomUIPanel;

    [Header("Unlock Button Manager")]
    [SerializeField] private UnlockButtonManager _unlockButtonManager;

    [Header("Tutorial Manager")]
    [SerializeField] private TutorialManager _tutorialManager;

    private RuntimeStateController _runtimeStateController;

    private const float StageAutoSaveInterval = 8f;
    private Coroutine _stageAutoSaveCoroutine;

    private void Awake()
    {
        if (_fishSpawnManager == null)
        {
            _fishSpawnManager = FindFirstObjectByType<FishSpawnManager>();
        }

        if (_stageDictionaryConvertor == null)
        {
            _stageDictionaryConvertor = FindFirstObjectByType<StageDictionaryConvertor>();
        }

        if (_fisherRuntimeContext == null)
        {
            _fisherRuntimeContext = FindFirstObjectByType<FisherRuntimeContext>();
        }

        if (_runtimeStateController == null)
        {
            _runtimeStateController = FindFirstObjectByType<RuntimeStateController>();
        }

        if (_unlockButtonManager == null)
        {
            _unlockButtonManager = FindFirstObjectByType<UnlockButtonManager>();
        }

        if (_tutorialManager == null)
        {
            _tutorialManager = FindFirstObjectByType<TutorialManager>();
        }
    }

    private void OnEnable()
    {
        //// ========== GameStart State ==========
        //RuntimeStateEventBus.OnGameStartStateEntered += OnGameStartStateEntered;
        //RuntimeStateEventBus.OnGameStartStateExited += OnGameStartStateExited;

        //// ========== Login State ==========
        //RuntimeStateEventBus.OnLoginStateEntered += OnLoginStateEntered;
        RuntimeStateEventBus.OnLoginStateExited += OnLoginStateExited;

        //// ========== OfflineReward State ==========
        //RuntimeStateEventBus.OnOfflineRewardStateEntered += OnOfflineRewardStateEntered;
        //RuntimeStateEventBus.OnOfflineRewardStateExited += OnOfflineRewardStateExited;

        //// ========== EquipmentUpgrade State ==========
        RuntimeStateEventBus.OnEquipmentUpgradeStateEntered += OnEquipmentUpgradeStateEntered;
        //RuntimeStateEventBus.OnEquipmentUpgradeStateExited += OnEquipmentUpgradeStateExited;

        //// ========== ShipUpgrade State ==========
        RuntimeStateEventBus.OnShipUpgradeStateEntered += OnShipUpgradeStateEntered;
        //RuntimeStateEventBus.OnShipUpgradeStateExited += OnShipUpgradeStateExited;

        //// ========== SailorUpgrade State ==========
        RuntimeStateEventBus.OnSailorUpgradeStateEntered += OnSailorUpgradeStateEntered;
        //RuntimeStateEventBus.OnSailorUpgradeStateExited += OnSailorUpgradeStateExited;

        //// ========== Cooking State ==========
        RuntimeStateEventBus.OnCookingStateEntered += OnCookingStateEntered;
        //RuntimeStateEventBus.OnCookingStateExited += OnCookingStateExited;

        //// ========== Inventory State ==========
        RuntimeStateEventBus.OnInventoryStateEntered += OnInventoryStateEntered;
        //RuntimeStateEventBus.OnInventoryStateExited += OnInventoryStateExited;

        //// ========== Shop State ==========
        RuntimeStateEventBus.OnShopStateEntered += OnShopStateEntered;
        //RuntimeStateEventBus.OnShopStateExited += OnShopStateExited;

        //// ========== Recruit State ==========
        RuntimeStateEventBus.OnRecruitStateEntered += OnRecruitStateEntered;
        //RuntimeStateEventBus.OnRecruitStateExited += OnRecruitStateExited;

        //// ========== Collection State ==========
        RuntimeStateEventBus.OnCollectionStateEntered += OnCollectionStateEntered;
        //RuntimeStateEventBus.OnCollectionStateExited += OnCollectionStateExited;

        // ========== MainStage State ==========
        RuntimeStateEventBus.OnMainStageStateEntered += OnMainStageStateEntered;
        RuntimeStateEventBus.OnMainStageStateExited += OnMainStageStateExited;

        //// ========== StageChange State ==========
        //RuntimeStateEventBus.OnStageChangeStateEntered += OnStageChangeStateEntered;
        //RuntimeStateEventBus.OnStageChangeStateExited += OnStageChangeStateExited;

        //// ========== BossStage State ==========
        //RuntimeStateEventBus.OnBossStageStateEntered += OnBossStageStateEntered;
        RuntimeStateEventBus.OnBossStageStateExited += OnBossStageStateExited;

        //// ========== MultiMatching State ==========
        //RuntimeStateEventBus.OnMultiMatchingStateEntered += OnMultiMatchingStateEntered;
        //RuntimeStateEventBus.OnMultiMatchingStateExited += OnMultiMatchingStateExited;

        //// ========== MultiStage State ==========
        //RuntimeStateEventBus.OnMultiStageStateEntered += OnMultiStageStateEntered;
        //RuntimeStateEventBus.OnMultiStageStateExited += OnMultiStageStateExited;

        // ========== FishSpawnManager Events ==========
        if (_fishSpawnManager != null)
        {
            _fishSpawnManager.OnStageChanged += OnStageChanged;
        }
    }

    private void OnDisable()
    {
        //// ========== GameStart State ==========
        //RuntimeStateEventBus.OnGameStartStateEntered -= OnGameStartStateEntered;
        //RuntimeStateEventBus.OnGameStartStateExited -= OnGameStartStateExited;

        //// ========== Login State ==========
        //RuntimeStateEventBus.OnLoginStateEntered -= OnLoginStateEntered;
        RuntimeStateEventBus.OnLoginStateExited -= OnLoginStateExited;

        //// ========== OfflineReward State ==========
        //RuntimeStateEventBus.OnOfflineRewardStateEntered -= OnOfflineRewardStateEntered;
        //RuntimeStateEventBus.OnOfflineRewardStateExited -= OnOfflineRewardStateExited;

        //// ========== EquipmentUpgrade State ==========
        RuntimeStateEventBus.OnEquipmentUpgradeStateEntered -= OnEquipmentUpgradeStateEntered;
        //RuntimeStateEventBus.OnEquipmentUpgradeStateExited -= OnEquipmentUpgradeStateExited;

        //// ========== ShipUpgrade State ==========
        RuntimeStateEventBus.OnShipUpgradeStateEntered -= OnShipUpgradeStateEntered;
        //RuntimeStateEventBus.OnShipUpgradeStateExited -= OnShipUpgradeStateExited;

        //// ========== SailorUpgrade State ==========
        RuntimeStateEventBus.OnSailorUpgradeStateEntered -= OnSailorUpgradeStateEntered;
        //RuntimeStateEventBus.OnSailorUpgradeStateExited -= OnSailorUpgradeStateExited;

        //// ========== Cooking State ==========
        RuntimeStateEventBus.OnCookingStateEntered -= OnCookingStateEntered;
        //RuntimeStateEventBus.OnCookingStateExited -= OnCookingStateExited;

        //// ========== Inventory State ==========
        RuntimeStateEventBus.OnInventoryStateEntered -= OnInventoryStateEntered;
        //RuntimeStateEventBus.OnInventoryStateExited -= OnInventoryStateExited;

        //// ========== Shop State ==========
        RuntimeStateEventBus.OnShopStateEntered -= OnShopStateEntered;
        //RuntimeStateEventBus.OnShopStateExited -= OnShopStateExited;

        //// ========== Recruit State ==========
        RuntimeStateEventBus.OnRecruitStateEntered -= OnRecruitStateEntered;
        //RuntimeStateEventBus.OnRecruitStateExited -= OnRecruitStateExited;

        //// ========== Collection State ==========
        RuntimeStateEventBus.OnCollectionStateEntered -= OnCollectionStateEntered;
        //RuntimeStateEventBus.OnCollectionStateExited -= OnCollectionStateExited;

        // ========== MainStage State ==========
        RuntimeStateEventBus.OnMainStageStateEntered -= OnMainStageStateEntered;
        RuntimeStateEventBus.OnMainStageStateExited -= OnMainStageStateExited;

        //// ========== StageChange State ==========
        //RuntimeStateEventBus.OnStageChangeStateEntered -= OnStageChangeStateEntered;
        //RuntimeStateEventBus.OnStageChangeStateExited -= OnStageChangeStateExited;

        //// ========== BossStage State ==========
        //RuntimeStateEventBus.OnBossStageStateEntered -= OnBossStageStateEntered;
        RuntimeStateEventBus.OnBossStageStateExited -= OnBossStageStateExited;

        //// ========== MultiMatching State ==========
        //RuntimeStateEventBus.OnMultiMatchingStateEntered -= OnMultiMatchingStateEntered;
        //RuntimeStateEventBus.OnMultiMatchingStateExited -= OnMultiMatchingStateExited;

        //// ========== MultiStage State ==========
        //RuntimeStateEventBus.OnMultiStageStateEntered -= OnMultiStageStateEntered;
        //RuntimeStateEventBus.OnMultiStageStateExited -= OnMultiStageStateExited;

        // ========== FishSpawnManager Events ==========
        if (_fishSpawnManager != null)
        {
            _fishSpawnManager.OnStageChanged -= OnStageChanged;
        }


        // 스테이지 정보 자동 저장 코루틴 종료
        if (_stageAutoSaveCoroutine != null)
        {
            StopCoroutine(_stageAutoSaveCoroutine);
            _stageAutoSaveCoroutine = null;
        }
    }


    // ==================== Application Quit Handler ====================
    // 게임 종료 시 현재 스테이지 snapshot을 저장합니다.
    private void OnApplicationQuit()
    {
        if (_runtimeStateController != null &&
            _runtimeStateController.CurrentState != RuntimeState.LOGIN && _runtimeStateController.CurrentState != RuntimeState.GAMESTART)
        {
            SaveCurrentStageSnapshot("ApplicationQuit");
        }
    }

    // ==================== RuntimeState Event Handlers ====================

    #region GameStart State
    private void OnGameStartStateEntered()
    {
        Debug.Log("[RuntimeDataSyncronizer] GameStart State Entered");
    }

    private void OnGameStartStateExited()
    {
        Debug.Log("[RuntimeDataSyncronizer] GameStart State Exited");
    }
    #endregion

    #region Login State
    private void OnLoginStateEntered()
    {
        Debug.Log("[RuntimeDataSyncronizer] Login State Entered");
    }

    private void OnLoginStateExited()
    {
        Debug.Log("[RuntimeDataSyncronizer] Login State Exited");
        LoadAndMoveToSavedStage(); // 로컬 JSON에서 저장된 StageId와 기여도를 불러와 MoveToStage 실행
        ResetStageAutoSaveTimer();
    }
    #endregion

    #region OfflineReward State
    private void OnOfflineRewardStateEntered()
    {
        Debug.Log("[RuntimeDataSyncronizer] OfflineReward State Entered");
    }

    private void OnOfflineRewardStateExited()
    {
        Debug.Log("[RuntimeDataSyncronizer] OfflineReward State Exited");
        // 오프라인 보상 수령 후 인벤토리, 화폐 데이터 새로고침
        RefreshInventoryData();
        RefreshMoneyData();
    }
    #endregion

    #region EquipmentUpgrade State
    private void OnEquipmentUpgradeStateEntered()
    {
        Debug.Log("[RuntimeDataSyncronizer] EquipmentUpgrade State Entered");
        _tutorialManager?.TryShowTutorial(TutorialType.Equipment);
    }

    private void OnEquipmentUpgradeStateExited()
    {
        Debug.Log("[RuntimeDataSyncronizer] EquipmentUpgrade State Exited");
        // 장비 업그레이드 종료 시 장비, 화폐 데이터 새로고침
        RefreshEquipmentData();
        RefreshMoneyData();
    }
    #endregion

    #region ShipUpgrade State
    private void OnShipUpgradeStateEntered()
    {
        Debug.Log("[RuntimeDataSyncronizer] ShipUpgrade State Entered");
        _tutorialManager?.TryShowTutorial(TutorialType.Ship);
    }

    private void OnShipUpgradeStateExited()
    {
        Debug.Log("[RuntimeDataSyncronizer] ShipUpgrade State Exited");
        // 선박 업그레이드 종료 시 선박, 화폐 데이터 새로고침
        RefreshShipData();
        RefreshMoneyData();
    }
    #endregion

    #region SailorUpgrade State
    private void OnSailorUpgradeStateEntered()
    {
        Debug.Log("[RuntimeDataSyncronizer] SailorUpgrade State Entered");
        _tutorialManager?.TryShowTutorial(TutorialType.Crew);
    }

    private void OnSailorUpgradeStateExited()
    {
        Debug.Log("[RuntimeDataSyncronizer] SailorUpgrade State Exited");
        // 선원 업그레이드 종료 시 선원, 화폐 데이터 새로고침
        RefreshCrewData();
        RefreshMoneyData();
    }
    #endregion

    #region Cooking State
    private void OnCookingStateEntered()
    {
        Debug.Log("[RuntimeDataSyncronizer] Cooking State Entered");
        _tutorialManager?.TryShowTutorial(TutorialType.Cooking);
    }

    private void OnCookingStateExited()
    {
        Debug.Log("[RuntimeDataSyncronizer] Cooking State Exited");
        // 요리 종료 시 요리, 인벤토리 데이터 새로고침
        RefreshCookingData();
        RefreshInventoryData();
    }
    #endregion

    #region Inventory State
    private void OnInventoryStateEntered()
    {
        Debug.Log("[RuntimeDataSyncronizer] Inventory State Entered");
        _tutorialManager?.TryShowTutorial(TutorialType.Inventory);
    }

    private void OnInventoryStateExited()
    {
        Debug.Log("[RuntimeDataSyncronizer] Inventory State Exited");
        // 인벤토리 종료 시 인벤토리, 화폐 데이터 새로고침
        RefreshInventoryData();
        RefreshMoneyData();
    }
    #endregion

    #region Shop State
    private void OnShopStateEntered()
    {
        Debug.Log("[RuntimeDataSyncronizer] Shop State Entered");
        _tutorialManager?.TryShowTutorial(TutorialType.Shop);
    }

    private void OnShopStateExited()
    {
        Debug.Log("[RuntimeDataSyncronizer] Shop State Exited");
        // 상점 종료 시 인벤토리, 화폐 데이터 새로고침
        RefreshInventoryData();
        RefreshMoneyData();
    }
    #endregion

    #region Recruit State
    private void OnRecruitStateEntered()
    {
        Debug.Log("[RuntimeDataSyncronizer] Recruit State Entered");
        _tutorialManager?.TryShowTutorial(TutorialType.Recruit);
    }

    private void OnRecruitStateExited()
    {
        Debug.Log("[RuntimeDataSyncronizer] Recruit State Exited");
        // 모집 종료 시 선원, 화폐 데이터 새로고침
        RefreshCrewData();
        RefreshMoneyData();
    }
    #endregion

    #region Collection State
    private void OnCollectionStateEntered()
    {
        Debug.Log("[RuntimeDataSyncronizer] Collection State Entered");
        _tutorialManager?.TryShowTutorial(TutorialType.Collection);
    }

    private void OnCollectionStateExited()
    {
        Debug.Log("[RuntimeDataSyncronizer] Collection State Exited");
        // 컬렉션 종료 시 인벤토리 데이터 새로고침
        RefreshInventoryData();
    }
    #endregion

    #region MainStage State
    private void OnMainStageStateEntered()
    {
        Debug.Log("[RuntimeDataSyncronizer] MainStage State Entered");
        _bottomUIPanel.SetActive(true); // 로그인 후 Bottom UI Panel 활성화
        LoadAndMoveToSavedStage();
        
        // UnlockButtonManager 갱신
        if (_unlockButtonManager != null)
        {
            _unlockButtonManager.RefreshUnlockState();
        }

        _tutorialManager?.TryShowTutorial(TutorialType.FirstLogin);
    }

    private void OnMainStageStateExited()
    {
        Debug.Log("[RuntimeDataSyncronizer] MainStage State Exited");
        FlushInventoryOnMainStageExit();
        SaveCurrentStageSnapshot("MainStageStateExited");
    }
    #endregion

    #region StageChange State
    private void OnStageChangeStateEntered()
    {
        Debug.Log("[RuntimeDataSyncronizer] StageChange State Entered");
    }

    private void OnStageChangeStateExited()
    {
        Debug.Log("[RuntimeDataSyncronizer] StageChange State Exited");
    }
    #endregion

    #region BossStage State
    private void OnBossStageStateEntered()
    {
        Debug.Log("[RuntimeDataSyncronizer] BossStage State Entered");
    }

    private void OnBossStageStateExited()
    {
        Debug.Log("[RuntimeDataSyncronizer] BossStage State Exited");
        // 보스 스테이지 종료 시 스테이지, 인벤토리, 화폐 데이터 새로고침
        SaveCurrentStageSnapshot("BossStageStateExited");
    }
    #endregion

    #region MultiMatching State
    private void OnMultiMatchingStateEntered()
    {
        Debug.Log("[RuntimeDataSyncronizer] MultiMatching State Entered");
    }

    private void OnMultiMatchingStateExited()
    {
        Debug.Log("[RuntimeDataSyncronizer] MultiMatching State Exited");
    }
    #endregion

    #region MultiStage State
    private void OnMultiStageStateEntered()
    {
        Debug.Log("[RuntimeDataSyncronizer] MultiStage State Entered");

    }

    private void OnMultiStageStateExited()
    {
        Debug.Log("[RuntimeDataSyncronizer] MultiStage State Exited");
        // 멀티 스테이지 종료 시 인벤토리, 화폐 데이터 새로고침
        RefreshInventoryData();
        RefreshMoneyData();
    }
    #endregion

    // ==================== Gateway Refresh Methods ====================

    /// <summary>
    /// 인벤토리 데이터 새로고침
    /// </summary>
    private void RefreshInventoryData()
    {
        if (PlayFabGateway.Instance?.Inventory == null)
        {
            Debug.LogWarning("[RuntimeDataSyncronizer] InventoryGateway가 없습니다.");
            return;
        }

        PlayFabGateway.Instance.Inventory.RefreshInventoryData(
            () => Debug.Log("[RuntimeDataSyncronizer] 인벤토리 데이터 새로고침 완료"),
            error => Debug.LogError($"[RuntimeDataSyncronizer] 인벤토리 데이터 새로고침 실패: {error?.ErrorMessage}")
        );
    }

    /// <summary>
    /// 선원 데이터 새로고침
    /// </summary>
    private void RefreshCrewData()
    {
        if (PlayFabGateway.Instance?.Crew == null)
        {
            Debug.LogWarning("[RuntimeDataSyncronizer] CrewGateway가 없습니다.");
            return;
        }

        PlayFabGateway.Instance.Crew.RefreshCrewData(
            () => Debug.Log("[RuntimeDataSyncronizer] 선원 데이터 새로고침 완료"),
            error => Debug.LogError($"[RuntimeDataSyncronizer] 선원 데이터 새로고침 실패: {error?.ErrorMessage}")
        );
    }

    /// <summary>
    /// 선박 데이터 새로고침
    /// </summary>
    private void RefreshShipData()
    {
        if (PlayFabGateway.Instance?.Ship == null)
        {
            Debug.LogWarning("[RuntimeDataSyncronizer] ShipGateway가 없습니다.");
            return;
        }

        PlayFabGateway.Instance.Ship.RefreshShipData(
            () => Debug.Log("[RuntimeDataSyncronizer] 선박 데이터 새로고침 완료"),
            error => Debug.LogError($"[RuntimeDataSyncronizer] 선박 데이터 새로고침 실패: {error?.ErrorMessage}")
        );
    }

    /// <summary>
    /// 장비 데이터 새로고침
    /// </summary>
    private void RefreshEquipmentData()
    {
        if (PlayFabGateway.Instance?.Equipment == null)
        {
            Debug.LogWarning("[RuntimeDataSyncronizer] EquipmentGateway가 없습니다.");
            return;
        }

        PlayFabGateway.Instance.Equipment.RefreshEquipmentData(
            () => Debug.Log("[RuntimeDataSyncronizer] 장비 데이터 새로고침 완료"),
            error => Debug.LogError($"[RuntimeDataSyncronizer] 장비 데이터 새로고침 실패: {error?.ErrorMessage}")
        );
    }

    /// <summary>
    /// 요리 데이터 새로고침
    /// </summary>
    private void RefreshCookingData()
    {
        if (PlayFabGateway.Instance?.Cooking == null)
        {
            Debug.LogWarning("[RuntimeDataSyncronizer] CookingGateway가 없습니다.");
            return;
        }

        PlayFabGateway.Instance.Cooking.RefreshCookingData(
            () => Debug.Log("[RuntimeDataSyncronizer] 요리 데이터 새로고침 완료"),
            error => Debug.LogError($"[RuntimeDataSyncronizer] 요리 데이터 새로고침 실패: {error?.ErrorMessage}")
        );
    }

    /// <summary>
    /// 화폐 데이터 새로고침
    /// </summary>
    private void RefreshMoneyData()
    {
        if (PlayFabGateway.Instance?.Money == null)
        {
            Debug.LogWarning("[RuntimeDataSyncronizer] MoneyGateway가 없습니다.");
            return;
        }

        PlayFabGateway.Instance.Money.RefreshMoneyData(
            () => Debug.Log("[RuntimeDataSyncronizer] 화폐 데이터 새로고침 완료"),
            error => Debug.LogError($"[RuntimeDataSyncronizer] 화폐 데이터 새로고침 실패: {error?.ErrorMessage}")
        );
    }

    /// <summary>
    /// 스테이지 데이터 새로고침
    /// </summary>
    private void RefreshStageData()
    {
        if (PlayFabGateway.Instance?.Stage == null)
        {
            Debug.LogWarning("[RuntimeDataSyncronizer] StageGateway가 없습니다.");
            return;
        }

        PlayFabGateway.Instance.Stage.RefreshStageData(
            () => Debug.Log("[RuntimeDataSyncronizer] 스테이지 데이터 새로고침 완료"),
            error => Debug.LogError($"[RuntimeDataSyncronizer] 스테이지 데이터 새로고침 실패: {error?.ErrorMessage}")
        );
    }

    /// <summary>
    /// 메인 스테이지 이탈 시 누적 인벤토리 queue를 서버에 반영한 뒤 최신 snapshot을 당겨옵니다.
    /// </summary>
    private void FlushInventoryOnMainStageExit()
    {
        if (PlayFabGateway.Instance?.Inventory == null)
        {
            Debug.LogWarning("[RuntimeDataSyncronizer] MainStage exit inventory flush skipped: InventoryGateway가 없습니다.");
            return;
        }

        PlayFabGateway.Instance.Inventory.FlushAll(
            _ =>
            {
                Debug.Log("[RuntimeDataSyncronizer] MainStage exit inventory flush complete.");
                RefreshInventoryData();
            },
            message => Debug.LogWarning("[RuntimeDataSyncronizer] MainStage exit inventory flush failed: " + message));
    }

    /// <summary>
    /// 현재 스테이지 snapshot을 로컬과 PlayFab에 저장합니다.
    /// </summary>
    private void SaveCurrentStageSnapshot(string source)
    {
        if (_fishSpawnManager == null || _fishSpawnManager.CurrentStage == null)
        {
            Debug.LogWarning("[RuntimeDataSyncronizer] " + source + " stage save skipped: 현재 스테이지가 없습니다.");
            return;
        }

        SaveStageToLocal(_fishSpawnManager.CurrentStage, _fishSpawnManager.TotalContribution);

        if (PlayFabGateway.Instance?.Stage == null)
        {
            Debug.LogWarning("[RuntimeDataSyncronizer] " + source + " PlayFab stage save skipped: StageGateway가 없습니다.");
            return;
        }

        PlayFabGateway.Instance.Stage.SaveStageData(
            _fishSpawnManager.CurrentStage.StageId,
            _fishSpawnManager.TotalContribution);

        // 자동 저장 타이머 리셋
        ResetStageAutoSaveTimer();
    }

    /// <summary>
    /// 현재 스테이지 snapshot을 로컬과 PlayFab에 주기적으로 저장하는 코루틴입니다. (StageAutoSaveInterval 간격)
    /// </summary>
    private IEnumerator StageAutoSaveCoroutine()
    {
        yield return new WaitForSeconds(StageAutoSaveInterval);

        SaveCurrentStageSnapshot("AutoSave");

        _stageAutoSaveCoroutine = StartCoroutine(StageAutoSaveCoroutine());
    }


    /// <summary>
    /// 자동 저장 코루틴을 초기화하고 재시작하여 중복 호출을 방지합니다.
    /// </summary>
    private void ResetStageAutoSaveTimer()
    {
        if (_stageAutoSaveCoroutine != null)
        {
            StopCoroutine(_stageAutoSaveCoroutine);
        }

        _stageAutoSaveCoroutine = StartCoroutine(StageAutoSaveCoroutine());
    }

    // ==================== Stage System ====================

    /// <summary>
    /// 로컬 JSON에서 저장된 StageId와 기여도를 불러와 MoveToStage 실행
    /// </summary>
    private void LoadAndMoveToSavedStage()
    {
        if (_fishSpawnManager == null)
        {
            Debug.LogError("[RuntimeDataSyncronizer] FishSpawnManager가 없습니다.");
            return;
        }

        if (PlayFabDataStore.Instance == null)
        {
            Debug.LogWarning("[RuntimeDataSyncronizer] PlayFabDataStore가 없습니다. 기본 스테이지로 시작합니다.");
            return;
        }

        try
        {
            // 로컬 JSON에서 플레이어 정보 가져오기
            PlayerInfo playerInfo = PlayFabDataStore.Instance.GetPlayerInfo();

            if (playerInfo?.stage == null)
            {
                Debug.LogWarning("[RuntimeDataSyncronizer] 저장된 스테이지 데이터가 없습니다. 기본 스테이지로 시작합니다.");
                return;
            }

            string savedStageId = playerInfo.stage.currentStageId;
            if (string.IsNullOrWhiteSpace(savedStageId))
            {
                Debug.LogWarning("[RuntimeDataSyncronizer] 저장된 StageId가 비어 있습니다. 기본 스테이지로 시작합니다.");
                return;
            }

            if (_stageDictionaryConvertor == null)
            {
                Debug.LogWarning("[RuntimeDataSyncronizer] StageDictionaryConvertor가 없습니다. 저장된 스테이지 이동을 건너뜁니다.");
                return;
            }

            float savedContribution = playerInfo.stage.contribution;

            Debug.Log($"[RuntimeDataSyncronizer] 로컬 데이터 로드 - StageId: {savedStageId}, 기여도: {savedContribution}");

            // StageId로 StageData ScriptableObject 로드
            StageData loadedStage = _stageDictionaryConvertor.GetStageDataById(savedStageId);

            if (loadedStage != null)
            {
                // FishSpawnManager.MoveToStage() 실행
                _fishSpawnManager.MoveToStage(loadedStage, savedContribution);
                Debug.Log($"[RuntimeDataSyncronizer] MoveToStage 실행 완료 - {loadedStage.DisplayName}, 기여도: {savedContribution}");
            }
            else
            {
                Debug.LogError($"[RuntimeDataSyncronizer] StageId '{savedStageId}'에 해당하는 StageData를 찾을 수 없습니다.");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[RuntimeDataSyncronizer] 스테이지 로드 중 오류 발생: {ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// 스테이지가 변경될 때 로컬 JSON에 저장
    /// </summary>
    private void OnStageChanged(StageData newStage)
    {
        if (newStage == null) return;

        SaveStageToLocal(newStage, _fishSpawnManager.TotalContribution);

        // UnlockButtonManager 갱신
        if (_unlockButtonManager != null)
        {
            _unlockButtonManager.RefreshUnlockState();
        }
    }

    /// <summary>
    /// 현재 스테이지 정보를 로컬 JSON에 저장
    /// </summary>
    public void SaveStageToLocal(StageData stage, float contribution)
    {
        if (stage == null)
        {
            Debug.LogError("[RuntimeDataSyncronizer] 저장할 스테이지가 null입니다.");
            return;
        }

        if (PlayFabDataStore.Instance == null)
        {
            Debug.LogError("[RuntimeDataSyncronizer] PlayFabDataStore가 없습니다.");
            return;
        }

        var playerInfo = PlayFabDataStore.Instance.GetPlayerInfo();

        string previousMaxStageId = playerInfo.stage?.maxStageId;

        if (string.IsNullOrEmpty(previousMaxStageId))
            previousMaxStageId = stage.StageId;

        string nextMaxStageId = IsStageFurther(stage.StageId, previousMaxStageId)
            ? stage.StageId
            : previousMaxStageId;

        var stageData = new StageJSONModel
        {
            currentStageId = stage.StageId,
            maxStageId = nextMaxStageId,
            contribution = contribution
        };

        PlayFabDataStore.Instance.UpdateStage(stageData);

        Debug.Log(
            $"[RuntimeDataSyncronizer] 스테이지 저장 완료 - ID: {stageData.currentStageId}, " +
            $"기여도: {stageData.contribution}, 최대 스테이지: {stageData.maxStageId}");
    }

    /// <summary>
    /// 수동으로 현재 스테이지를 저장 (외부에서 호출 가능)
    /// </summary>
    public void SaveCurrentStage()
    {
        if (_fishSpawnManager?.CurrentStage != null)
        {
            SaveStageToLocal(_fishSpawnManager.CurrentStage, _fishSpawnManager.TotalContribution);
        }
    }

    private bool IsStageFurther(string a, string b)
    {
        if (!TryParseStageId(a, out int mapA, out int stageA))
            return false;

        if (!TryParseStageId(b, out int mapB, out int stageB))
            return true;

        if (mapA != mapB)
            return mapA > mapB;

        return stageA > stageB;
    }

    private bool TryParseStageId(string stageId, out int map, out int stage)
    {
        map = 1;
        stage = 1;

        if (string.IsNullOrEmpty(stageId))
            return false;

        string[] split = stageId.Split('-');

        if (split.Length != 2)
            return false;

        if (!int.TryParse(split[0], out map))
            return false;

        if (split[1].Equals("boss", System.StringComparison.OrdinalIgnoreCase))
        {
            stage = 999;
            return true;
        }

        return int.TryParse(split[1], out stage);
    }
}
