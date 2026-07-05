using RMS.Fishing;
using RMS.UI;
using Runtime;
using UnityEngine;


namespace UI
{
    public class RuntimeUIController : MonoBehaviour
    {
        [Header("Runtime UI")]
        [SerializeField] private GameObject _gameStartUI;
        [SerializeField] private GameObject _loginUI;
        [SerializeField] private GameObject _offlineRewardUI;
        [SerializeField] private GameObject _equipmentUpgradeUI;
        [SerializeField] private GameObject _shipUpgradeUI;
        [SerializeField] private GameObject _sailorUpgradeUI;
        [SerializeField] private GameObject _cookingUI;
        [SerializeField] private GameObject _inventoryUI;
        [SerializeField] private GameObject _shopUI;
        [SerializeField] private GameObject _recruitUI;
        [SerializeField] private GameObject _collectionUI;
        [SerializeField] private GameObject _mainStageUI;
        [SerializeField] private GameObject _stageChangeUI;
        [SerializeField] private GameObject _bossStageUI;
        [SerializeField] private GameObject _multiMatchingUI;
        [SerializeField] private GameObject _multiStageUI;
        [SerializeField] private GameObject _biteInteractButton;
        [SerializeField] private GameObject _bossStagePanel;
        [SerializeField] private GameObject _multiButton;

        [SerializeField] private BossStageUI _bossStageUIController;
        [SerializeField] private FishSpawnManager _fishSpawnManager;
        [SerializeField] private BossStageActivator _bossStageActivator;

        private GameObject _currentRuntimeUI;
        private bool _bossStageInitialized = false;


        public void SetActiveRuntimeUI(RuntimeState newRuntimeState)
        {
            if (_currentRuntimeUI != null)
            {
                _currentRuntimeUI.SetActive(false);
            }

            _currentRuntimeUI = newRuntimeState switch
            {
                RuntimeState.GAMESTART => _gameStartUI,
                RuntimeState.LOGIN => _loginUI,
                RuntimeState.OFFLINEREWARD => _offlineRewardUI,
                RuntimeState.EQUIPMENTUPGRADE => _equipmentUpgradeUI,
                RuntimeState.SHIPUPGRADE => _shipUpgradeUI,
                RuntimeState.SAILORUPGRADE => _sailorUpgradeUI,
                RuntimeState.COOKING => _cookingUI,
                RuntimeState.INVENTORY => _inventoryUI,
                RuntimeState.SHOP => _shopUI,
                RuntimeState.RECRUIT => _recruitUI,
                RuntimeState.COLLECTION => _collectionUI,
                RuntimeState.MAINSTAGE => _mainStageUI,
                RuntimeState.STAGECHANGE => _stageChangeUI,
                RuntimeState.BOSSSTAGE => _bossStageUI,
                RuntimeState.MULTIMATCHING => _multiMatchingUI,
                RuntimeState.MULTISTAGE => _multiStageUI,
                _ => null
            };

            if (_currentRuntimeUI != null)
            {
                _currentRuntimeUI.SetActive(true);
            }

            // 보스 UI 초기화 — 최초 진입 시에만 ShowBossStage 호출
            if (newRuntimeState == RuntimeState.BOSSSTAGE)
            {
                if (!_bossStageInitialized && _bossStageUIController != null)
                {
                    var bossData = _fishSpawnManager?.CurrentBossData;
                    if (bossData != null)
                    {
                        _bossStageUIController.ShowBossStage(bossData);
                        _bossStageInitialized = true;
                    }
                }
            }
            else
            {
                bool bossIsActive = _bossStageActivator != null && _bossStageActivator.IsBossStageActive;
                if (!bossIsActive)
                    _bossStageInitialized = false;
            }

            // MAINSTAGE일 때만 BiteInteractButton 활성화
            if (_biteInteractButton != null)
                _biteInteractButton.SetActive(
                    newRuntimeState == RuntimeState.MAINSTAGE ||
                    newRuntimeState == RuntimeState.BOSSSTAGE);

            // 보스 스테이지 중이면 MultiButton을 항상 숨긴다 (RuntimeState와 무관하게 강제)
            bool isBossActive = _bossStageActivator != null && _bossStageActivator.IsBossStageActive;
            if (_multiButton != null)
                _multiButton.SetActive(!isBossActive && newRuntimeState == RuntimeState.MAINSTAGE);
        }
    }
}
